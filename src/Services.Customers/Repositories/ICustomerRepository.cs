using Services.Customers.Domain;

namespace Services.Customers.Repositories;

public interface ICustomerRepository
{
    Task<Customer> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task InsertAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<bool> UpsertAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
