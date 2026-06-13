namespace Services.ReadModelBuilder.Services.ChangeStream;

public interface IChangeStreamService<TEntity>
    where TEntity : class
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
