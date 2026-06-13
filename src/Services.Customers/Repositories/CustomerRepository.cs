using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.Customers.Domain;
using Services.Customers.Options;

namespace Services.Customers.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly IMongoCollection<Customer> _collection;

    public CustomerRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var db = client.GetDatabase(options.Value.Database);
        _collection = db.GetCollection<Customer>(options.Value.Collection);
    }

    public Task<Customer> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(c => c.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return Array.Empty<Customer>();
        }
        var filter = Builders<Customer>.Filter.In(c => c.Id, ids);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(FilterDefinition<Customer>.Empty)
            .SortBy(c => c.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(customer, cancellationToken: cancellationToken);
    }

    public async Task<bool> ReplaceAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(
            c => c.Id == customer.Id,
            customer,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> UpsertAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(
            c => c.Id == customer.Id,
            customer,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteOneAsync(c => c.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
