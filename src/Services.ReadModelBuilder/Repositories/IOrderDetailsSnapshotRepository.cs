using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;

namespace Services.ReadModelBuilder.Repositories;

public interface IOrderDetailsSnapshotRepository
{
    Task InsertOneAsync(OrderDetailsSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<OrderDetailsSnapshot> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<UpdateResult> UpdateOneAsync(FilterDefinition<OrderDetailsSnapshot> filter, UpdateDefinition<OrderDetailsSnapshot> update, CancellationToken cancellationToken = default);

    IFindFluent<OrderDetailsSnapshot, OrderDetailsSnapshot> Find(FilterDefinition<OrderDetailsSnapshot> filter);
}
