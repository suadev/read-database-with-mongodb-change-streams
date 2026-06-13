using MongoDB.Bson.Serialization.Attributes;

namespace Services.ReadModelBuilder.Domain;

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

    public OrderDetail ToOrderDetail()
    {
        return new OrderDetail
        {
            Id = Id,
            CustomerId = CustomerId,
            OrderNumber = OrderNumber,
            Status = Status,
            PaymentMethod = PaymentMethod,
            ShippingAddress = ShippingAddress,
            BillingAddress = BillingAddress,
            Currency = Currency,
            SubTotal = SubTotal,
            DiscountAmount = DiscountAmount,
            ShippingCost = ShippingCost,
            TaxAmount = TaxAmount,
            TotalAmount = TotalAmount,
            ItemCount = ItemCount,
            Items = Items?.Select(i => new OrderItemDetail
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal,
                // ProductName / Sku populated later by IOrderEnricherService
            }).ToList(),
            Notes = Notes,
            PlacedAt = PlacedAt,
            ShippedAt = ShippedAt,
            DeliveredAt = DeliveredAt,
            CancelledAt = CancelledAt,
            CancellationReason = CancellationReason,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
