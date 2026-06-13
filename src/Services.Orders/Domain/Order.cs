using MongoDB.Bson.Serialization.Attributes;

namespace Services.Orders.Domain;

// Source-of-truth Order document. Schema MUST match Services.ReadModelBuilder.Domain.Order
// byte-for-byte — the listener deserializes documents from this exact collection via MongoDB
// change streams. Adding/renaming a field here requires the matching change in the listener.
public class Order
{
    [BsonId]
    [BsonElement("_id")]
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string OrderNumber { get; set; }

    public string Status { get; set; }

    public string PaymentMethod { get; set; }

    public string ShippingAddress { get; set; }

    public string BillingAddress { get; set; }

    public string Currency { get; set; }

    public decimal SubTotal { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? ShippingCost { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public int ItemCount { get; set; }

    public List<OrderItem> Items { get; set; }

    public string Notes { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public DateTimeOffset? ShippedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
