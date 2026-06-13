namespace Services.ReadModelBuilder.Services.Clients;

public interface IProductServiceClient
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
