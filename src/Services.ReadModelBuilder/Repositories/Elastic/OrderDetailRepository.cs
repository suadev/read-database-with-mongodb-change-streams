using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Options;

namespace Services.ReadModelBuilder.Repositories.Elastic;

public class OrderDetailRepository : IOrderDetailRepository
{
    private readonly ElasticsearchClient _client;
    private readonly string _index;
    private readonly ILogger<OrderDetailRepository> _logger;

    public OrderDetailRepository(
        ElasticsearchClient client,
        IOptions<ElasticOptions> elasticOptions,
        ILogger<OrderDetailRepository> logger)
    {
        _client = client;
        _index = elasticOptions.Value.OrderDetailsIndexName;
        _logger = logger;
    }

    public async Task<OrderDetail> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetAsync<OrderDetail>(id.ToString(), g => g.Index(_index), cancellationToken);
        if (!response.IsValidResponse)
        {
            // Missing document returns IsValidResponse=false with Found=false; treat both as "not found".
            if (response.Found == false)
            {
                return null;
            }

            ThrowOnInvalid(response, $"GetAsync({id})");
        }

        return response.Source;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.CountAsync<OrderDetail>(c => c.Indices(_index), cancellationToken);
        if (!response.IsValidResponse)
        {
            ThrowOnInvalid(response, "CountAsync");
        }
        return response.Count;
    }

    public async Task UpsertAsync(OrderDetail orderDetail, CancellationToken cancellationToken = default)
    {
        var response = await _client.IndexAsync(
            orderDetail,
            i => i.Index(_index).Id(orderDetail.Id.ToString()),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            ThrowOnInvalid(response, $"IndexAsync({orderDetail.Id})");
        }
    }

    public async Task<bool> PartialUpdateAsync(Guid id, IReadOnlyDictionary<string, object> fields, CancellationToken cancellationToken = default)
    {
        if (fields == null || fields.Count == 0)
        {
            return true;
        }

        // Translate PascalCase property names to the camelCase keys Elastic stores by default.
        var partialDoc = new Dictionary<string, object>(fields.Count);
        foreach (var kvp in fields)
        {
            partialDoc[JsonNamingPolicy.CamelCase.ConvertName(kvp.Key)] = kvp.Value;
        }

        var response = await _client.UpdateAsync<OrderDetail, Dictionary<string, object>>(
            _index,
            id.ToString(),
            u => u.Doc(partialDoc).DocAsUpsert(false),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            // 404 (document_missing_exception) is not an error for our pipeline — handler decides what to do.
            if (response.ApiCallDetails?.HttpStatusCode == 404)
            {
                return false;
            }

            ThrowOnInvalid(response, $"UpdateAsync({id})");
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<OrderDetail>(id.ToString(), d => d.Index(_index), cancellationToken);
        if (!response.IsValidResponse)
        {
            if (response.ApiCallDetails?.HttpStatusCode == 404)
            {
                return false;
            }

            ThrowOnInvalid(response, $"DeleteAsync({id})");
        }

        return response.Result == Result.Deleted;
    }

    public async Task BulkUpsertAsync(IReadOnlyCollection<OrderDetail> orderDetails, CancellationToken cancellationToken = default)
    {
        if (orderDetails == null || orderDetails.Count == 0)
        {
            return;
        }

        var response = await _client.BulkAsync(
            b =>
            {
                b.Index(_index);
                foreach (var orderDetail in orderDetails)
                {
                    b.Index(orderDetail, op => op.Id(orderDetail.Id.ToString()));
                }
            },
            cancellationToken);

        if (!response.IsValidResponse || response.Errors)
        {
            var failures = response.ItemsWithErrors?.ToList();
            var summary = failures is { Count: > 0 }
                ? string.Join("; ", failures.Take(5).Select(f => $"{f.Id}:{f.Error?.Reason}"))
                : response.DebugInformation;

            throw new InvalidOperationException($"BulkAsync upsert failed: {summary}");
        }
    }

    public async Task RefreshIndexAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.Indices.RefreshAsync(r => r.Indices(_index), cancellationToken);
        if (!response.IsValidResponse)
        {
            ThrowOnInvalid(response, $"RefreshIndexAsync({_index})");
        }
    }

    private void ThrowOnInvalid<TResponse>(TResponse response, string operation)
        where TResponse : global::Elastic.Transport.Products.Elasticsearch.ElasticsearchResponse
    {
        var status = response.ApiCallDetails?.HttpStatusCode;
        var reason = response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation;
        _logger.LogError("Elasticsearch {Operation} failed with status {Status}: {Reason}", operation, status, reason);
        throw new InvalidOperationException($"Elasticsearch {operation} failed: status={status}, reason={reason}");
    }
}
