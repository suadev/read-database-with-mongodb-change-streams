using Services.Orders.Contracts;
using Services.Orders.Domain;
using Services.Orders.Repositories;
using Services.Orders.Services.Clients;

namespace Services.Orders.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/orders");

        group.MapGet("", async (IOrderRepository repo, int skip, int take, CancellationToken ct) =>
        {
            if (take <= 0) take = 50;
            if (take > 500) take = 500;
            if (skip < 0) skip = 0;
            var list = await repo.ListAsync(skip, take, ct);
            return Results.Ok(list.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.FindByIdAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(ToDto(order));
        });

        group.MapPost("", async (CreateOrderRequest req, IOrderRepository repo, IProductServiceClient productClient, CancellationToken ct) =>
        {
            if (req.Items is null || req.Items.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one item is required." });
            }

            // Fetch the latest price from the product service for every distinct product in the cart.
            var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();
            var priceMap = await productClient.GetPricesAsync(productIds, ct);

            var missing = productIds.Where(id => !priceMap.ContainsKey(id)).ToList();
            if (missing.Count > 0)
            {
                return Results.BadRequest(new { error = "Unknown productId(s).", productIds = missing });
            }

            var items = req.Items.Select(i =>
            {
                var price = priceMap[i.ProductId].Price;
                return new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = price,
                    LineTotal = price * i.Quantity,
                };
            }).ToList();

            var subTotal = items.Sum(i => i.LineTotal);
            var totalAmount = subTotal
                - (req.DiscountAmount ?? 0m)
                + (req.ShippingCost ?? 0m)
                + (req.TaxAmount ?? 0m);

            var now = DateTimeOffset.UtcNow;
            var order = new Order
            {
                Id = req.Id ?? Guid.NewGuid(),
                CustomerId = req.CustomerId,
                OrderNumber = $"ORD-{now:yyyyMMddHHmmssfff}",
                Status = OrderStatus.Placed,
                PaymentMethod = req.PaymentMethod,
                Currency = req.Currency,
                ShippingAddress = req.ShippingAddress,
                BillingAddress = req.BillingAddress,
                Items = items,
                ItemCount = items.Sum(i => i.Quantity),
                SubTotal = subTotal,
                DiscountAmount = req.DiscountAmount,
                ShippingCost = req.ShippingCost,
                TaxAmount = req.TaxAmount,
                TotalAmount = totalAmount,
                Notes = req.Notes,
                PlacedAt = now,
                CreatedAt = now,
            };

            await repo.InsertAsync(order, ct);
            return Results.Created($"/orders/{order.Id}", ToDto(order));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateOrderRequest req, IOrderRepository repo, IProductServiceClient productClient, CancellationToken ct) =>
        {
            var existing = await repo.FindByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();

            if (req.Items is null || req.Items.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one item is required." });
            }

            var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();
            var priceMap = await productClient.GetPricesAsync(productIds, ct);
            var missing = productIds.Where(pid => !priceMap.ContainsKey(pid)).ToList();
            if (missing.Count > 0)
            {
                return Results.BadRequest(new { error = "Unknown productId(s).", productIds = missing });
            }

            var items = req.Items.Select(i =>
            {
                var price = priceMap[i.ProductId].Price;
                return new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = price,
                    LineTotal = price * i.Quantity,
                };
            }).ToList();

            var subTotal = items.Sum(i => i.LineTotal);
            var totalAmount = subTotal
                - (req.DiscountAmount ?? 0m)
                + (req.ShippingCost ?? 0m)
                + (req.TaxAmount ?? 0m);

            existing.PaymentMethod = req.PaymentMethod;
            existing.Currency = req.Currency;
            existing.ShippingAddress = req.ShippingAddress;
            existing.BillingAddress = req.BillingAddress;
            existing.Items = items;
            existing.ItemCount = items.Sum(i => i.Quantity);
            existing.SubTotal = subTotal;
            existing.DiscountAmount = req.DiscountAmount;
            existing.ShippingCost = req.ShippingCost;
            existing.TaxAmount = req.TaxAmount;
            existing.TotalAmount = totalAmount;
            existing.Notes = req.Notes;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            var ok = await repo.ReplaceAsync(existing, ct);
            return ok ? Results.Ok(ToDto(existing)) : Results.NotFound();
        });

        // Status-only update — single $set; exercises the listener's partial-update path.
        group.MapPatch("/{id:guid}/status", async (Guid id, UpdateStatusRequest req, IOrderRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Status) || !OrderStatus.All.Contains(req.Status))
            {
                return Results.BadRequest(new { error = "Invalid status.", allowed = OrderStatus.All });
            }

            var ok = await repo.UpdateStatusAsync(id, req.Status, DateTimeOffset.UtcNow, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var ok = await repo.DeleteAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        return endpoints;
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.CustomerId,
        o.OrderNumber,
        o.Status,
        o.PaymentMethod,
        o.Currency,
        o.SubTotal,
        o.DiscountAmount,
        o.ShippingCost,
        o.TaxAmount,
        o.TotalAmount,
        o.ItemCount,
        o.Items?.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPrice, i.LineTotal)).ToList() ?? [],
        o.ShippingAddress,
        o.BillingAddress,
        o.Notes,
        o.PlacedAt,
        o.ShippedAt,
        o.DeliveredAt,
        o.CancelledAt,
        o.CancellationReason,
        o.CreatedAt,
        o.UpdatedAt);
}
