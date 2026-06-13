using MongoDB.Bson.Serialization.Attributes;

namespace Services.Products.Domain;

public class Product
{
    [BsonId]
    [BsonElement("_id")]
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Sku { get; set; }

    public decimal Price { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
