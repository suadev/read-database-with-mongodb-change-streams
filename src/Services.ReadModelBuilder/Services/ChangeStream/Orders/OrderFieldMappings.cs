using Services.ReadModelBuilder.Domain;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

// When you add a new field to Order/OrderDetail that should propagate on update,
// add an entry here — otherwise updates to that field are silently ignored.
public static class OrderFieldMappings
{
    public static readonly Dictionary<string, Func<Order, KeyValuePair<string, object>>> FieldMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Order.CustomerId)] = order => new(nameof(OrderDetail.CustomerId), order.CustomerId),
            [nameof(Order.OrderNumber)] = order => new(nameof(OrderDetail.OrderNumber), order.OrderNumber),
            [nameof(Order.Status)] = order => new(nameof(OrderDetail.Status), order.Status),
            [nameof(Order.PaymentMethod)] = order => new(nameof(OrderDetail.PaymentMethod), order.PaymentMethod),
            [nameof(Order.ShippingAddress)] = order => new(nameof(OrderDetail.ShippingAddress), order.ShippingAddress),
            [nameof(Order.BillingAddress)] = order => new(nameof(OrderDetail.BillingAddress), order.BillingAddress),
            [nameof(Order.Currency)] = order => new(nameof(OrderDetail.Currency), order.Currency),
            [nameof(Order.SubTotal)] = order => new(nameof(OrderDetail.SubTotal), order.SubTotal),
            [nameof(Order.DiscountAmount)] = order => new(nameof(OrderDetail.DiscountAmount), order.DiscountAmount),
            [nameof(Order.ShippingCost)] = order => new(nameof(OrderDetail.ShippingCost), order.ShippingCost),
            [nameof(Order.TaxAmount)] = order => new(nameof(OrderDetail.TaxAmount), order.TaxAmount),
            [nameof(Order.TotalAmount)] = order => new(nameof(OrderDetail.TotalAmount), order.TotalAmount),
            [nameof(Order.ItemCount)] = order => new(nameof(OrderDetail.ItemCount), order.ItemCount),
            [nameof(Order.Notes)] = order => new(nameof(OrderDetail.Notes), order.Notes),
            [nameof(Order.PlacedAt)] = order => new(nameof(OrderDetail.PlacedAt), order.PlacedAt),
            [nameof(Order.ShippedAt)] = order => new(nameof(OrderDetail.ShippedAt), order.ShippedAt),
            [nameof(Order.DeliveredAt)] = order => new(nameof(OrderDetail.DeliveredAt), order.DeliveredAt),
            [nameof(Order.CancelledAt)] = order => new(nameof(OrderDetail.CancelledAt), order.CancelledAt),
            [nameof(Order.CancellationReason)] = order => new(nameof(OrderDetail.CancellationReason), order.CancellationReason),
            [nameof(Order.UpdatedAt)] = order => new(nameof(OrderDetail.UpdatedAt), order.UpdatedAt),
        };
}
