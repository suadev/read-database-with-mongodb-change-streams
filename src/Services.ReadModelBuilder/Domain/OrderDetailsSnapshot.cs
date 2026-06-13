using MongoDB.Bson.Serialization.Attributes;
using Services.ReadModelBuilder.Domain.Constants;

namespace Services.ReadModelBuilder.Domain;

public class OrderDetailsSnapshot
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public SnapshotStatus Status { get; set; }

    public long TotalCount { get; set; }

    public long ProcessedCount { get; set; }

    public int BatchSize { get; set; }

    public string ErrorMessage { get; set; }

    [BsonIgnore]
    public TimeSpan? Duration => FinishedAt.HasValue ? FinishedAt.Value - StartedAt : DateTime.UtcNow - StartedAt;
}
