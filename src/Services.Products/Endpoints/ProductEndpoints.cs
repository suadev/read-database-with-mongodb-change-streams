using Services.Products.Contracts;
using Services.Products.Domain;
using Services.Products.Repositories;

namespace Services.Products.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/products");

        group.MapGet("", async (IProductRepository repo, int skip, int take, CancellationToken ct) =>
        {
            if (take <= 0)
                take = 50;
            if (take > 500)
                take = 500;
            if (skip < 0)
                skip = 0;

            var list = await repo.ListAsync(skip, take, ct);

            return Results.Ok(list.Select(ToDto));
        });

        group.MapGet("/batch", async (IProductRepository repo, string ids, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return Results.Ok(Array.Empty<ProductDto>());
            }

            var parsed = new List<Guid>();
            foreach (var raw in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(raw, out var id))
                {
                    parsed.Add(id);
                }
            }

            var products = await repo.FindByIdsAsync(parsed, ct);

            return Results.Ok(products.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid id, IProductRepository repo, CancellationToken ct) =>
        {
            var product = await repo.FindByIdAsync(id, ct);
            return product is null ? Results.NotFound() : Results.Ok(ToDto(product));
        });

        group.MapPost("", async (CreateProductRequest req, IProductRepository repo, CancellationToken ct) =>
        {
            var product = new Product
            {
                Id = req.Id ?? Guid.NewGuid(),
                Name = req.Name,
                Sku = req.Sku,
                Price = req.Price,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await repo.InsertAsync(product, ct);

            return Results.Created($"/products/{product.Id}", ToDto(product));
        });

        // PUT is "create-or-replace" — idempotent. Existing doc keeps CreatedAt; new docs get it set now.
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest req, IProductRepository repo, CancellationToken ct) =>
        {
            var existing = await repo.FindByIdAsync(id, ct);
            var now = DateTimeOffset.UtcNow;

            var product = new Product
            {
                Id = id,
                Name = req.Name,
                Sku = req.Sku,
                Price = req.Price,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = existing is null ? null : now,
            };

            var replaced = await repo.UpsertAsync(product, ct);
            return replaced
                ? Results.Ok(ToDto(product))
                : Results.Created($"/products/{product.Id}", ToDto(product));
        });

        group.MapDelete("/{id:guid}", async (Guid id, IProductRepository repo, CancellationToken ct) =>
        {
            var ok = await repo.DeleteAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        return endpoints;
    }

    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Sku, p.Price);
}
