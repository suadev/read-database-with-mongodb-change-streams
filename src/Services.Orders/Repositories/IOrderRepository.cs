using Services.Orders.Domain;

namespace Services.Orders.Repositories;

public interface IOrderRepository
{
    Task<Order> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task InsertAsync(Order order, CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(Order order, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(Guid id, string status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
