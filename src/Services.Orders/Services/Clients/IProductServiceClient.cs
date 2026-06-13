namespace Services.Orders.Services.Clients;

public record ProductPriceDto(Guid Id, string Name, string Sku, decimal Price);

public interface IProductServiceClient
{
    Task<IReadOnlyDictionary<Guid, ProductPriceDto>> GetPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
}
