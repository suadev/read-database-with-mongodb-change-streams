namespace Services.ReadModelBuilder.Services.ChangeStream;

public interface IEnricherService<TEntity, TEnrichedEntity>
    where TEntity : class
    where TEnrichedEntity : class
{
    Task<List<TEnrichedEntity>> EnrichAllAsync(List<TEntity> entities, CancellationToken cancellationToken);
}
