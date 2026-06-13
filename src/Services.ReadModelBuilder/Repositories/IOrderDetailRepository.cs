using Services.ReadModelBuilder.Domain;

namespace Services.ReadModelBuilder.Repositories;

public interface IOrderDetailRepository
{
    Task<OrderDetail> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<long> CountAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(OrderDetail orderDetail, CancellationToken cancellationToken = default);

    Task<bool> PartialUpdateAsync(Guid id, IReadOnlyDictionary<string, object> fields, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(IReadOnlyCollection<OrderDetail> orderDetails, CancellationToken cancellationToken = default);

    Task RefreshIndexAsync(CancellationToken cancellationToken = default);
}
