namespace Services.ReadModelBuilder.Services.ChangeStream;

public interface ISnapshotService<TEntity>
    where TEntity : class
{
    Task<string> InitiateSnapshotAsync(CancellationToken cancellationToken = default);

    Task<TEntity> GetSnapshotStatusAsync(string snapshotId, CancellationToken cancellationToken = default);
}
