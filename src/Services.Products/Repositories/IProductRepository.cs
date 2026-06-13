using Services.Products.Domain;

namespace Services.Products.Repositories;

public interface IProductRepository
{
    Task<Product> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task InsertAsync(Product product, CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(Product product, CancellationToken cancellationToken = default);

    Task<bool> UpsertAsync(Product product, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
