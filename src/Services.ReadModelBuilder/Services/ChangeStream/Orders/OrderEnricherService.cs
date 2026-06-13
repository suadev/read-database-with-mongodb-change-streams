using Microsoft.Extensions.Caching.Memory;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Exceptions;
using Services.ReadModelBuilder.Services.Clients;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

public class OrderEnricherService : IOrderEnricherService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly ICustomerServiceClient _customerClient;
    private readonly IProductServiceClient _productClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrderEnricherService> _logger;

    public OrderEnricherService(
        ICustomerServiceClient customerClient,
        IProductServiceClient productClient,
        IMemoryCache cache,
        ILogger<OrderEnricherService> logger)
    {
        _customerClient = customerClient;
        _productClient = productClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<OrderDetail>> EnrichAllAsync(List<Order> orders, CancellationToken cancellationToken)
    {
        var orderDetails = orders.Select(o => o.ToOrderDetail()).ToList();

        if (orderDetails.Count == 0)
        {
            return orderDetails;
        }

        var customerIds = orders
            .Select(o => o.CustomerId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var productIds = orders
            .SelectMany(o => o.Items ?? [])
            .Select(i => i.ProductId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var customerTask = GetCachedAsync(
            customerIds,
            "Customer",
            ids => _customerClient.GetCustomersAsync(ids, cancellationToken),
            c => c.Id,
            cancellationToken);

        var productTask = GetCachedAsync(
            productIds,
            "Product",
            ids => _productClient.GetProductsAsync(ids, cancellationToken),
            p => p.Id,
            cancellationToken);

        await Task.WhenAll(customerTask, productTask);

        var customersById = customerTask.Result.ToDictionary(c => c.Id);
        var productsById = productTask.Result.ToDictionary(p => p.Id);

        foreach (var orderDetail in orderDetails)
        {
            if (customersById.TryGetValue(orderDetail.CustomerId, out var customer))
            {
                orderDetail.CustomerName = customer.Name;
                orderDetail.CustomerEmail = customer.Email;
                orderDetail.CustomerPhone = customer.Phone;
            }

            if (orderDetail.Items != null)
            {
                foreach (var item in orderDetail.Items)
                {
                    if (productsById.TryGetValue(item.ProductId, out var product))
                    {
                        item.ProductName = product.Name;
                        item.Sku = product.Sku;
                    }
                }
            }
        }

        return orderDetails;
    }

    private async Task<List<TDto>> GetCachedAsync<TDto>(
        IReadOnlyCollection<Guid> ids,
        string entityName,
        Func<IReadOnlyCollection<Guid>, Task<IReadOnlyList<TDto>>> fetcher,
        Func<TDto, Guid> idSelector,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var hits = new List<TDto>(ids.Count);
        var misses = new List<Guid>();
        foreach (var id in ids)
        {
            if (_cache.TryGetValue<TDto>(CacheKey(entityName, id), out var cached) && cached is not null)
            {
                hits.Add(cached);
            }
            else
            {
                misses.Add(id);
            }
        }

        if (misses.Count == 0)
        {
            return hits;
        }

        IReadOnlyList<TDto> fetched;
        try
        {
            fetched = await fetcher(misses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {EntityName} batch for {Count} ids.", entityName, misses.Count);
            throw new OrderEnrichmentException(entityName, $"Failed to fetch {entityName} batch ({misses.Count} ids).", ex);
        }

        foreach (var dto in fetched)
        {
            _cache.Set(CacheKey(entityName, idSelector(dto)), dto, CacheTtl);
            hits.Add(dto);
        }

        return hits;
    }

    private static string CacheKey(string entityName, Guid id) => $"{entityName}:{id:N}";
}
