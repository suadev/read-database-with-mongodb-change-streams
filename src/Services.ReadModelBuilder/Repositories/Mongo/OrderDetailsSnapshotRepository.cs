using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Domain.Constants;
using Services.ReadModelBuilder.Options;

namespace Services.ReadModelBuilder.Repositories.Mongo;

public class OrderDetailsSnapshotRepository : IOrderDetailsSnapshotRepository
{
    private readonly IMongoCollection<OrderDetailsSnapshot> _collection;

    public OrderDetailsSnapshotRepository(IMongoClient mongoClient, IOptions<MongoOptions> mongoOptions)
    {
        var database = mongoClient.GetDatabase(mongoOptions.Value.Database);
        _collection = database.GetCollection<OrderDetailsSnapshot>(MongoConstants.OrderDetailsSnapshotsCollectionName);
    }

    public Task InsertOneAsync(OrderDetailsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(snapshot, cancellationToken: cancellationToken);
    }

    public Task<OrderDetailsSnapshot> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UpdateResult> UpdateOneAsync(FilterDefinition<OrderDetailsSnapshot> filter, UpdateDefinition<OrderDetailsSnapshot> update, CancellationToken cancellationToken = default)
    {
        return _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public IFindFluent<OrderDetailsSnapshot, OrderDetailsSnapshot> Find(FilterDefinition<OrderDetailsSnapshot> filter)
    {
        return _collection.Find(filter);
    }
}
