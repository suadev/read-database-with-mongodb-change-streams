using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Services.ReadModelBuilder.Domain;

public class ResumeToken
{
    [BsonId]
    public string Id { get; set; }

    public string CollectionName { get; set; }

    public BsonDocument Token { get; set; } = new();

    public DateTimeOffset LastUpdated { get; set; }

    public ResumeToken()
    {
    }

    public ResumeToken(string collectionName, BsonDocument token)
    {
        Id = $"resume_token_{collectionName}";
        CollectionName = collectionName;
        Token = token;
        LastUpdated = DateTime.UtcNow;
    }
}
