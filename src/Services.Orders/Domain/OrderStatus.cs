namespace Services.Orders.Domain;

// String constants rather than an enum so the field round-trips as a plain string in MongoDB
// (matches what the listener's field mapping for Order.Status expects).
public static class OrderStatus
{
    public const string Placed = "Placed";
    public const string Confirmed = "Confirmed";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly HashSet<string> All =
        new(StringComparer.OrdinalIgnoreCase) { Placed, Confirmed, Shipped, Delivered, Cancelled };
}
