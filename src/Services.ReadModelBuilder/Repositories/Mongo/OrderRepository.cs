using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Domain.Constants;
using Services.ReadModelBuilder.Options;

namespace Services.ReadModelBuilder.Repositories.Mongo;

public class OrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> _collection;

    public OrderRepository(IMongoClient mongoClient, IOptions<MongoOptions> mongoOptions)
    {
        var database = mongoClient.GetDatabase(mongoOptions.Value.Database);
        _collection = database.GetCollection<Order>(MongoConstants.OrderCollectionName);
    }

    public Task<Order> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<long> CountDocumentsAsync(FilterDefinition<Order> filter, CancellationToken cancellationToken = default)
    {
        return _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public IFindFluent<Order, Order> Find(FilterDefinition<Order> filter)
    {
        return _collection.Find(filter);
    }

    public Task<IChangeStreamCursor<ChangeStreamDocument<Order>>> WatchAsync(
        PipelineDefinition<ChangeStreamDocument<Order>, ChangeStreamDocument<Order>> pipeline,
        ChangeStreamOptions options,
        CancellationToken cancellationToken = default)
    {
        return _collection.WatchAsync(pipeline, options, cancellationToken);
    }
}
