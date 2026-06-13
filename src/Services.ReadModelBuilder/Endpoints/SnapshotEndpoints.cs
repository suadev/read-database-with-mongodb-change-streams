using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Services.ChangeStream;

namespace Services.ReadModelBuilder.Endpoints;

public static class SnapshotEndpoints
{
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/init-snapshot", async (
            ISnapshotService<OrderDetailsSnapshot> snapshotService,
            CancellationToken cancellationToken) =>
        {
            var snapshotId = await snapshotService.InitiateSnapshotAsync(cancellationToken);
            return Results.Accepted(value: new { id = snapshotId });
        });

        return endpoints;
    }
}
