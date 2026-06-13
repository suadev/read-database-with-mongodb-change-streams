using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Domain.Constants;
using Services.ReadModelBuilder.Exceptions;
using Services.ReadModelBuilder.Options;
using Services.ReadModelBuilder.Repositories;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

public class OrderSnapshotService : ISnapshotService<OrderDetailsSnapshot>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderDetailRepository _orderDetailRepository;
    private readonly IOrderDetailsSnapshotRepository _snapshotRepository;
    private readonly IOrderEnricherService _enricherService;
    private readonly SnapshotOptions _snapshotOptions;
    private readonly ILogger<OrderSnapshotService> _logger;

    public OrderSnapshotService(
        IOrderRepository orderRepository,
        IOrderDetailRepository orderDetailRepository,
        IOrderDetailsSnapshotRepository snapshotRepository,
        IOrderEnricherService enricherService,
        IOptions<SnapshotOptions> snapshotOptions,
        ILogger<OrderSnapshotService> logger)
    {
        _orderRepository = orderRepository;
        _orderDetailRepository = orderDetailRepository;
        _snapshotRepository = snapshotRepository;
        _enricherService = enricherService;
        _snapshotOptions = snapshotOptions.Value;
        _logger = logger;
    }

    public async Task<string> InitiateSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating new order details snapshot");

        var filter = Builders<OrderDetailsSnapshot>.Filter.Or(
            Builders<OrderDetailsSnapshot>.Filter.Eq(s => s.Status, SnapshotStatus.Ready),
            Builders<OrderDetailsSnapshot>.Filter.Eq(s => s.Status, SnapshotStatus.Running)
        );
        var activeSnapshot = await _snapshotRepository
            .Find(filter)
            .SortByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSnapshot != null)
        {
            throw new InvalidOperationException($"Snapshot {activeSnapshot.Id} is already running. Cannot start multiple snapshots simultaneously.");
        }

        var totalDocumentCount = await _orderRepository.CountDocumentsAsync(FilterDefinition<Order>.Empty, cancellationToken);

        _logger.LogInformation("Found {TotalCount} orders to process in snapshot", totalDocumentCount);

        if (totalDocumentCount == 0)
        {
            _logger.LogWarning("No orders found in collection. Snapshot not needed.");
            throw new InvalidOperationException("No orders found in collection to snapshot.");
        }

        var snapshot = new OrderDetailsSnapshot
        {
            StartedAt = DateTime.UtcNow,
            Status = SnapshotStatus.Ready,
            TotalCount = totalDocumentCount,
            ProcessedCount = 0,
            BatchSize = _snapshotOptions.BatchSize > 0 ? _snapshotOptions.BatchSize : 100
        };

        await _snapshotRepository.InsertOneAsync(snapshot, cancellationToken);

        _logger.LogInformation("Created snapshot tracking document {SnapshotId}", snapshot.Id);

        // Fire-and-forget background processing.
        _ = ProcessSnapshotAsync(snapshot.Id, cancellationToken).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                var exception = task.Exception?.GetBaseException() ?? task.Exception;
                _logger.LogError(exception, "Background snapshot processing failed for {SnapshotId}", snapshot.Id);
            }
        }, TaskScheduler.Default);

        return snapshot.Id;
    }

    private async Task ProcessSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting background snapshot processing for {SnapshotId}.", snapshotId);

        try
        {
            await UpdateSnapshotStatusAsync(snapshotId, SnapshotStatus.Running, cancellationToken: cancellationToken);

            var snapshot = await GetSnapshotStatusAsync(snapshotId, cancellationToken);
            var processedCount = 0L;
            var batchNumber = 1;

            _logger.LogInformation("Processing {TotalCount} orders in batches of {BatchSize}.", snapshot.TotalCount, snapshot.BatchSize);

            // Composite cursor: (CreatedAt, Id). Handles same-timestamp collisions and deletions
            // that a single-key cursor would skip past.
            DateTimeOffset? lastProcessedCreatedAt = null;
            Guid? lastProcessedId = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Processing batch {BatchNumber}: processed {ProcessedCount} documents so far.", batchNumber, processedCount);

                FilterDefinition<Order> filter;
                if (lastProcessedCreatedAt == null)
                {
                    filter = FilterDefinition<Order>.Empty;
                }
                else
                {
                    filter = Builders<Order>.Filter.Or(
                        Builders<Order>.Filter.Gt(x => x.CreatedAt, lastProcessedCreatedAt),
                        Builders<Order>.Filter.And(
                            Builders<Order>.Filter.Eq(x => x.CreatedAt, lastProcessedCreatedAt),
                            Builders<Order>.Filter.Gt(x => x.Id, lastProcessedId)
                        )
                    );
                }

                var orders = await _orderRepository
                    .Find(filter)
                    .Sort(Builders<Order>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.Id))
                    .Limit(snapshot.BatchSize)
                    .ToListAsync(cancellationToken);

                if (orders.Count == 0)
                {
                    _logger.LogInformation("No more orders found. Processing completed. Total processed: {ProcessedCount}.", processedCount);
                    break;
                }

                var orderDetails = await _enricherService.EnrichAllAsync(orders, cancellationToken);

                var now = DateTime.UtcNow;
                foreach (var orderDetail in orderDetails)
                {
                    orderDetail.LocalCreatedAt = now;
                    orderDetail.LocalUpdatedAt = now;
                }

                var batchProcessed = 0;
                if (orderDetails.Count > 0)
                {
                    try
                    {
                        await _orderDetailRepository.BulkUpsertAsync(orderDetails, cancellationToken);
                        _logger.LogInformation("Bulk upsert completed: {Count} OrderDetails indexed.", orderDetails.Count);
                        batchProcessed = orderDetails.Count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error during bulk upsert in snapshot {SnapshotId}.", snapshotId);
                        // Continue processing — batchProcessed remains 0 for this batch.
                    }
                }

                processedCount += batchProcessed;
                await UpdateSnapshotStatusAsync(snapshotId, SnapshotStatus.Running, processedCount: processedCount, cancellationToken: cancellationToken);

                var lastOrder = orders[^1];
                lastProcessedCreatedAt = lastOrder.CreatedAt;
                lastProcessedId = lastOrder.Id;

                batchNumber++;

                _logger.LogInformation("Completed batch {BatchNumber}: processed {BatchProcessed}/{BatchFound} orders. Total processed: {ProcessedCount}.",
                    batchNumber - 1, batchProcessed, orders.Count, processedCount);
            }

            // Force a single refresh so the just-written bulk batches become visible to _count
            // (Elasticsearch's default 1s refresh interval would otherwise return a stale 0).
            // One refresh at the end is far cheaper than refresh-per-batch.
            await _orderDetailRepository.RefreshIndexAsync(cancellationToken);

            var finalCount = await _orderDetailRepository.CountAsync(cancellationToken);

            await UpdateSnapshotStatusAsync(snapshotId, SnapshotStatus.Done, DateTime.UtcNow, null, finalCount, finalCount, cancellationToken);

            _logger.LogInformation("Snapshot {SnapshotId} completed. Final OrderDetails count: {FinalCount} (initial source count was {InitialTotalCount}, processed {ProcessedCount}).",
                snapshotId, finalCount, snapshot.TotalCount, processedCount);
        }
        catch (OrderEnrichmentException ex)
        {
            _logger.LogError(ex, "Order enrichment failed for batch in snapshot {SnapshotId} — Operation: {Operation}", snapshotId, ex.Operation);
            await UpdateSnapshotStatusAsync(snapshotId, SnapshotStatus.Failed, DateTime.UtcNow, $"External service failure: {ex.Operation} — {ex.Message}", null, null, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snapshot {SnapshotId} failed with error: {ErrorMessage}.", snapshotId, ex.Message);
            await UpdateSnapshotStatusAsync(snapshotId, SnapshotStatus.Failed, DateTime.UtcNow, ex.Message, null, null, cancellationToken);
        }
    }

    public async Task<OrderDetailsSnapshot> GetSnapshotStatusAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository.FindByIdAsync(snapshotId, cancellationToken);
        return snapshot ?? throw new ArgumentException($"Snapshot with ID {snapshotId} not found.", nameof(snapshotId));
    }

    private async Task UpdateSnapshotStatusAsync(string snapshotId, SnapshotStatus status, DateTime? finishedAt = null,
        string errorMessage = null, long? finalTotalCount = null, long? processedCount = null, CancellationToken cancellationToken = default)
    {
        var updateBuilder = Builders<OrderDetailsSnapshot>.Update.Set(s => s.Status, status);
        if (finishedAt.HasValue)
        {
            updateBuilder = updateBuilder.Set(s => s.FinishedAt, finishedAt.Value);
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            updateBuilder = updateBuilder.Set(s => s.ErrorMessage, errorMessage);
        }

        if (finalTotalCount.HasValue)
        {
            updateBuilder = updateBuilder.Set(s => s.TotalCount, finalTotalCount.Value);
        }

        if (processedCount.HasValue)
        {
            updateBuilder = updateBuilder.Set(s => s.ProcessedCount, processedCount.Value);
        }

        var filter = Builders<OrderDetailsSnapshot>.Filter.Eq(s => s.Id, snapshotId);
        await _snapshotRepository.UpdateOneAsync(filter, updateBuilder, cancellationToken);
    }
}
