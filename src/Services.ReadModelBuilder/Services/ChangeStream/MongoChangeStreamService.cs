using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Options;
using Services.ReadModelBuilder.Repositories;

namespace Services.ReadModelBuilder.Services.ChangeStream;

public class MongoChangeStreamService<TEntity> : IChangeStreamService<TEntity>
    where TEntity : class
{
    private readonly IRepository<TEntity> _repository;
    private readonly IResumeTokenRepository _resumeTokenRepository;
    private readonly IChangeStreamEventHandler<TEntity> _changeEventHandler;
    private readonly MongoOptions _mongoOptions;
    private readonly string _collectionName;
    private readonly ILogger<MongoChangeStreamService<TEntity>> _logger;
    private Timer _resumeTokenRefreshTimer;

    // Poison message tracking: documentId -> consecutive failure count
    private readonly Dictionary<string, int> _failureTracker = [];

    public MongoChangeStreamService(
        IRepository<TEntity> repository,
        IResumeTokenRepository resumeTokenRepository,
        IChangeStreamEventHandler<TEntity> changeEventHandler,
        IOptions<MongoOptions> mongoOptions,
        ILogger<MongoChangeStreamService<TEntity>> logger,
        string collectionName)
    {
        _repository = repository;
        _resumeTokenRepository = resumeTokenRepository;
        _changeEventHandler = changeEventHandler;
        _mongoOptions = mongoOptions.Value;
        _logger = logger;
        _collectionName = collectionName;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting MongoDB Change Stream Service for collection {CollectionName}.", _collectionName);

            var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<TEntity>>().Match(change =>
                    change.OperationType == ChangeStreamOperationType.Insert ||
                    change.OperationType == ChangeStreamOperationType.Update ||
                    change.OperationType == ChangeStreamOperationType.Replace ||
                    change.OperationType == ChangeStreamOperationType.Delete);

            var changeStreamOptions = new ChangeStreamOptions
            {
                FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
            };

            var resumeToken = await LoadResumeTokenAsync();
            if (resumeToken != null)
            {
                changeStreamOptions.ResumeAfter = resumeToken;
                _logger.LogInformation("Resuming change stream from saved position for collection {CollectionName}.", _collectionName);
            }
            else
            {
                _logger.LogInformation("Starting change stream from current time (no resume token found) for collection {CollectionName}.", _collectionName);
            }

            using var changeStream = await CreateChangeStreamWithFallback(pipeline, changeStreamOptions, cancellationToken);

            StartResumeTokenRefreshTimer(cancellationToken);

            await foreach (var change in changeStream.ToAsyncEnumerable().WithCancellation(cancellationToken))
            {
                var documentId = change.DocumentKey?.GetValue("_id", "unknown");
                var documentIdString = documentId?.ToString() ?? "unknown";

                _logger.LogInformation("Processing change stream event: {OperationType} for document {DocumentId} in collection {CollectionName}",
                    change.OperationType, documentId, _collectionName);

                try
                {
                    await _changeEventHandler.HandleAsync(change, cancellationToken);
                    await SaveResumeTokenAsync(change.ResumeToken);

                    if (_failureTracker.ContainsKey(documentIdString))
                    {
                        _failureTracker.Remove(documentIdString);
                        _logger.LogInformation("Successfully processed previously failed document {DocumentId} in collection {CollectionName}. Cleared failure tracker.",
                            documentId, _collectionName);
                    }

                    _logger.LogInformation("Successfully processed and saved resume token for document {DocumentId} in collection {CollectionName}", documentId, _collectionName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process change stream event for document {DocumentId} in collection {CollectionName}.",
                        documentId, _collectionName);

                    var isInfraError = IsInfrastructureError(ex);

                    if (isInfraError)
                    {
                        _logger.LogWarning("Infrastructure error detected for document {DocumentId}. Will retry indefinitely without counting toward poison message limit. Error: {ErrorType}",
                            documentId, ex.GetType().Name);
                    }
                    else
                    {
                        _logger.LogWarning("Data/Logic error detected for document {DocumentId}. This counts toward poison message limit. Error: {ErrorType}",
                            documentId, ex.GetType().Name);
                    }

                    if (!isInfraError && _mongoOptions.MaxConsecutiveFailuresPerDocument > 0)
                    {
                        if (!_failureTracker.ContainsKey(documentIdString))
                        {
                            _failureTracker[documentIdString] = 0;
                        }
                        _failureTracker[documentIdString]++;

                        var failureCount = _failureTracker[documentIdString];
                        _logger.LogWarning("Document {DocumentId} has failed {FailureCount} time(s) with data/logic errors. Max allowed: {MaxFailures}",
                            documentId, failureCount, _mongoOptions.MaxConsecutiveFailuresPerDocument);

                        if (failureCount >= _mongoOptions.MaxConsecutiveFailuresPerDocument)
                        {
                            _logger.LogCritical(
                                "POISON MESSAGE DETECTED: Document {DocumentId} in collection {CollectionName} has failed {FailureCount} consecutive times with data/logic errors. " +
                                "Skipping this document and moving resume token forward to prevent pipeline blockage. Exception: {Exception}",
                                documentId, _collectionName, failureCount, ex.Message);

                            await SaveResumeTokenAsync(change.ResumeToken);
                            _failureTracker.Remove(documentIdString);
                            continue;
                        }
                    }

                    // Re-throw to stop change stream processing and trigger service restart.
                    // The hosted service's exponential-backoff loop will resume from the last successfully saved token.
                    _logger.LogError("Service will restart to retry from last successful position.");
                    throw;
                }
            }

            _logger.LogInformation("MongoDB Change Stream Service completed for collection {Collection} in database {Database}.",
                _collectionName, _mongoOptions.Database);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("MongoDB Change Stream Service was canceled during shutdown for collection {CollectionName}.", _collectionName);
            // Don't rethrow — cancellation is expected during graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MongoDB Change Stream Service for collection {CollectionName}.", _collectionName);
            throw;
        }
    }

    private static bool IsInfrastructureError(Exception ex)
    {
        var exType = ex.GetType().Name;
        var exMessage = ex.Message?.ToLowerInvariant() ?? string.Empty;

        if (ex is MongoConnectionException or
            MongoAuthenticationException or
            MongoExecutionTimeoutException)
        {
            return true;
        }

        if (ex is TimeoutException || exType.Contains("Timeout"))
        {
            return true;
        }

        if (exType.Contains("Socket") || exType.Contains("Network") ||
            exMessage.Contains("connection") || exMessage.Contains("network") ||
            exMessage.Contains("timeout") || exMessage.Contains("unreachable"))
        {
            return true;
        }

        return ex is OperationCanceledException or TaskCanceledException;
    }

    private async Task<IAsyncCursor<ChangeStreamDocument<TEntity>>> CreateChangeStreamWithFallback(
        PipelineDefinition<ChangeStreamDocument<TEntity>, ChangeStreamDocument<TEntity>> pipeline,
        ChangeStreamOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.WatchAsync(pipeline, options, cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Message.Contains("resume"))
        {
            _logger.LogError(ex, "Resume point is no longer available in the oplog for collection {CollectionName}. " +
                "This typically happens when the resume token is too old and the oplog has rotated. " +
                "Clearing resume token and starting from current time to continue streaming.", _collectionName);

            await ClearResumeTokenAsync();

            var changeStreamOptions = new ChangeStreamOptions
            {
                FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
            };

            _logger.LogInformation("Starting change stream from current time after clearing invalid resume token for collection {CollectionName}.", _collectionName);

            return await _repository.WatchAsync(pipeline, changeStreamOptions, cancellationToken);
        }
    }

    private async Task<BsonDocument> LoadResumeTokenAsync()
    {
        try
        {
            var resumeTokenDoc = await _resumeTokenRepository.FindByCollectionNameAsync(_collectionName);
            if (resumeTokenDoc == null || resumeTokenDoc.Token == null)
            {
                _logger.LogInformation("No resume token found in database for collection {CollectionName}.", _collectionName);
                return null;
            }

            _logger.LogInformation("Loaded resume token from MongoDB collection ResumeTokens for collection {CollectionName}.", resumeTokenDoc.CollectionName);
            return resumeTokenDoc.Token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load resume token from MongoDB for collection {CollectionName}. Service will restart to retry from last successful position", _collectionName);
            throw;
        }
    }

    private async Task SaveResumeTokenAsync(BsonDocument resumeToken)
    {
        try
        {
            var resumeTokenDoc = new ResumeToken(_collectionName, resumeToken);

            var filter = Builders<ResumeToken>.Filter.Eq(rt => rt.CollectionName, _collectionName);
            await _resumeTokenRepository.ReplaceOneAsync(filter, resumeTokenDoc, new ReplaceOptions { IsUpsert = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warning: Could not save resume token: {Message}.", ex.Message);
        }
    }

    private async Task ClearResumeTokenAsync()
    {
        try
        {
            var resumeTokenDoc = new ResumeToken(_collectionName, null);
            var filter = Builders<ResumeToken>.Filter.Eq(rt => rt.CollectionName, _collectionName);
            await _resumeTokenRepository.ReplaceOneAsync(filter, resumeTokenDoc, new ReplaceOptions { IsUpsert = true });
            _logger.LogInformation("Cleared invalid resume token from database for collection {CollectionName}.", _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warning: Could not clear resume token: {Message}.", ex.Message);
        }
    }

    private void StartResumeTokenRefreshTimer(CancellationToken cancellationToken)
    {
        if (!_mongoOptions.EnableResumeTokenRefresh)
        {
            _logger.LogInformation("Resume token refresh is disabled in configuration for collection {CollectionName}.", _collectionName);
            return;
        }

        var refreshInterval = TimeSpan.FromHours(_mongoOptions.ResumeTokenRefreshIntervalHours);
        _resumeTokenRefreshTimer = new Timer(
            callback: async _ => await RefreshResumeTokenIfNeeded(cancellationToken),
            state: null,
            dueTime: refreshInterval,
            period: refreshInterval);

        _logger.LogInformation("Resume token refresh enabled for collection {CollectionName}. Interval: {IntervalHours} hours, Max token age: {MaxAgeHours} hours",
            _collectionName, _mongoOptions.ResumeTokenRefreshIntervalHours, _mongoOptions.MaxResumeTokenAgeHours);
    }

    private async Task RefreshResumeTokenIfNeeded(CancellationToken cancellationToken)
    {
        try
        {
            var resumeTokenDoc = await _resumeTokenRepository.FindByCollectionNameAsync(_collectionName, cancellationToken);
            if (resumeTokenDoc != null)
            {
                var tokenAge = DateTime.UtcNow - resumeTokenDoc.LastUpdated;
                var maxAge = TimeSpan.FromHours(_mongoOptions.MaxResumeTokenAgeHours);

                if (tokenAge > maxAge)
                {
                    _logger.LogInformation("Resume token for collection {CollectionName} is {TokenAgeHours:F1} hours old (max: {MaxAgeHours} hours). Refreshing from current position.",
                        _collectionName, tokenAge.TotalHours, _mongoOptions.MaxResumeTokenAgeHours);

                    await RefreshFromCurrentPosition(cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Resume token for collection {CollectionName} is still fresh (age: {TokenAgeHours:F1} hours)", _collectionName, tokenAge.TotalHours);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resume token refresh for collection {CollectionName}", _collectionName);
        }
    }

    private async Task RefreshFromCurrentPosition(CancellationToken cancellationToken)
    {
        try
        {
            var options = new ChangeStreamOptions
            {
                FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
            };

            var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<TEntity>>();
            using var tempChangeStream = await _repository.WatchAsync(pipeline, options, cancellationToken);

            var resumeToken = tempChangeStream.GetResumeToken();
            if (resumeToken != null)
            {
                await SaveResumeTokenAsync(resumeToken);
                _logger.LogInformation("Successfully refreshed resume token for collection {CollectionName}", _collectionName);
            }
            else
            {
                _logger.LogWarning("Could not obtain resume token for collection {CollectionName}. Clearing token.", _collectionName);
                await ClearResumeTokenAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh resume token for collection {CollectionName}. Clearing token to restart from current time.", _collectionName);
            await ClearResumeTokenAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _resumeTokenRefreshTimer?.Dispose();

        _logger.LogInformation("MongoDB Change Stream Service stopped successfully for collection {CollectionName}.", _collectionName);
        return Task.CompletedTask;
    }
}
