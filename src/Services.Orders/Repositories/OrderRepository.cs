using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.Orders.Domain;
using Services.Orders.Options;

namespace Services.Orders.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> _collection;

    public OrderRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var db = client.GetDatabase(options.Value.Database);
        _collection = db.GetCollection<Order>(options.Value.Collection);
    }

    public Task<Order> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(o => o.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(FilterDefinition<Order>.Empty)
            .SortByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(Order order, CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(order, cancellationToken: cancellationToken);
    }

    public async Task<bool> ReplaceAsync(Order order, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(
            o => o.Id == order.Id,
            order,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
        return result.MatchedCount > 0;
    }

    // Targeted $set so the change-stream UpdateDescription only reports Status + UpdatedAt.
    // This is what exercises the listener's partial-update path (via OrderFieldMappings).
    public async Task<bool> UpdateStatusAsync(Guid id, string status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        var update = Builders<Order>.Update
            .Set(o => o.Status, status)
            .Set(o => o.UpdatedAt, updatedAt);
        var result = await _collection.UpdateOneAsync(o => o.Id == id, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteOneAsync(o => o.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
