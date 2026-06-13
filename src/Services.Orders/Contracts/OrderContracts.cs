namespace Services.Orders.Contracts;

public record OrderItemRequest(Guid ProductId, int Quantity);

public record CreateOrderRequest(
    Guid? Id,
    Guid CustomerId,
    string PaymentMethod,
    string Currency,
    string ShippingAddress,
    string BillingAddress,
    List<OrderItemRequest> Items,
    string Notes,
    decimal? DiscountAmount,
    decimal? ShippingCost,
    decimal? TaxAmount);

public record UpdateOrderRequest(
    string PaymentMethod,
    string Currency,
    string ShippingAddress,
    string BillingAddress,
    List<OrderItemRequest> Items,
    string Notes,
    decimal? DiscountAmount,
    decimal? ShippingCost,
    decimal? TaxAmount);

public record UpdateStatusRequest(string Status, string CancellationReason);

public record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDto(
    Guid Id,
    Guid CustomerId,
    string OrderNumber,
    string Status,
    string PaymentMethod,
    string Currency,
    decimal SubTotal,
    decimal? DiscountAmount,
    decimal? ShippingCost,
    decimal? TaxAmount,
    decimal TotalAmount,
    int ItemCount,
    List<OrderItemDto> Items,
    string ShippingAddress,
    string BillingAddress,
    string Notes,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CancelledAt,
    string CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
