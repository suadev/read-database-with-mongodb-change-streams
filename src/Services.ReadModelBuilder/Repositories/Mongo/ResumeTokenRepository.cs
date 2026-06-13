using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Domain.Constants;
using Services.ReadModelBuilder.Options;

namespace Services.ReadModelBuilder.Repositories.Mongo;

public class ResumeTokenRepository : IResumeTokenRepository
{
    private readonly IMongoCollection<ResumeToken> _collection;

    public ResumeTokenRepository(IMongoClient mongoClient, IOptions<MongoOptions> mongoOptions)
    {
        var database = mongoClient.GetDatabase(mongoOptions.Value.Database);
        _collection = database.GetCollection<ResumeToken>(MongoConstants.ResumeTokenCollectionName);
    }

    public Task<ResumeToken> FindByCollectionNameAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return _collection.Find(x => x.CollectionName == collectionName).FirstOrDefaultAsync(cancellationToken);
    }

    public Task ReplaceOneAsync(FilterDefinition<ResumeToken> filter, ResumeToken resumeToken, ReplaceOptions options = null, CancellationToken cancellationToken = default)
    {
        return _collection.ReplaceOneAsync(filter, resumeToken, options, cancellationToken);
    }
}
