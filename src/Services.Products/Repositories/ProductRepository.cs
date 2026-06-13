using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.Products.Domain;
using Services.Products.Options;

namespace Services.Products.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IMongoCollection<Product> _collection;

    public ProductRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var db = client.GetDatabase(options.Value.Database);
        _collection = db.GetCollection<Product>(options.Value.Collection);
    }

    public Task<Product> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(p => p.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return Array.Empty<Product>();
        }
        var filter = Builders<Product>.Filter.In(p => p.Id, ids);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(FilterDefinition<Product>.Empty)
            .SortBy(p => p.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(Product product, CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(product, cancellationToken: cancellationToken);
    }

    public async Task<bool> ReplaceAsync(Product product, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(
            p => p.Id == product.Id,
            product,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> UpsertAsync(Product product, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(
            p => p.Id == product.Id,
            product,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteOneAsync(p => p.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
