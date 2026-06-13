using MongoDB.Bson.Serialization.Attributes;

namespace Services.Customers.Domain;

public class Customer
{
    [BsonId]
    [BsonElement("_id")]
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
