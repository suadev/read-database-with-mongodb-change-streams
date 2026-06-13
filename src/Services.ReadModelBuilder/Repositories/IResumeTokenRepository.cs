using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;

namespace Services.ReadModelBuilder.Repositories;

public interface IResumeTokenRepository
{
    Task<ResumeToken> FindByCollectionNameAsync(string collectionName, CancellationToken cancellationToken = default);

    Task ReplaceOneAsync(FilterDefinition<ResumeToken> filter, ResumeToken resumeToken, ReplaceOptions options = null, CancellationToken cancellationToken = default);
}
