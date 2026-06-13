using MongoDB.Driver;

namespace Services.ReadModelBuilder.Repositories;

public interface IRepository<TEntity>
    where TEntity : class
{
    Task<TEntity> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<long> CountDocumentsAsync(FilterDefinition<TEntity> filter, CancellationToken cancellationToken = default);

    IFindFluent<TEntity, TEntity> Find(FilterDefinition<TEntity> filter);

    Task<IChangeStreamCursor<ChangeStreamDocument<TEntity>>> WatchAsync(
        PipelineDefinition<ChangeStreamDocument<TEntity>, ChangeStreamDocument<TEntity>> pipeline,
        ChangeStreamOptions options,
        CancellationToken cancellationToken = default);
}
