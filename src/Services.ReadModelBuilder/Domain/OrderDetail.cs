namespace Services.ReadModelBuilder.Domain;

// Read model document persisted to Elasticsearch.
public class OrderDetail
{
    // Mapped 1:1 from Order
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

    public string Notes { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public DateTimeOffset? ShippedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    // Enrichment fields (populated by IOrderEnricherService — placeholders for now)
    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public string CustomerPhone { get; set; }

    public List<OrderItemDetail> Items { get; set; }

    // Bookkeeping
    public DateTime LocalCreatedAt { get; set; }

    public DateTime LocalUpdatedAt { get; set; }
}

public class OrderItemDetail
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
