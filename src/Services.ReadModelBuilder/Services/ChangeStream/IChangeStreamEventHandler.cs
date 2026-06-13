using MongoDB.Driver;

namespace Services.ReadModelBuilder.Services.ChangeStream;

public interface IChangeStreamEventHandler<T>
{
    Task HandleAsync(ChangeStreamDocument<T> changeDocument, CancellationToken cancellationToken = default);
}
