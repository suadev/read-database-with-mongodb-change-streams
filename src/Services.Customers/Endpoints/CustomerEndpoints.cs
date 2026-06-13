using Services.Customers.Contracts;
using Services.Customers.Domain;
using Services.Customers.Repositories;

namespace Services.Customers.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/customers");

        group.MapGet("", async (ICustomerRepository repo, int skip, int take, CancellationToken ct) =>
        {
            if (take <= 0)
                take = 50;
            if (take > 500) take = 500;

            if (skip < 0) skip = 0;

            var list = await repo.ListAsync(skip, take, ct);

            return Results.Ok(list.Select(ToDto));
        });

        // Batch lookup is the hot path for the enricher: GET /customers/batch?ids=guid1,guid2,...
        group.MapGet("/batch", async (ICustomerRepository repo, string ids, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return Results.Ok(Array.Empty<CustomerDto>());
            }

            var parsed = new List<Guid>();
            foreach (var raw in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(raw, out var id))
                {
                    parsed.Add(id);
                }
            }

            var customers = await repo.FindByIdsAsync(parsed, ct);

            return Results.Ok(customers.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid id, ICustomerRepository repo, CancellationToken ct) =>
        {
            var customer = await repo.FindByIdAsync(id, ct);

            return customer is null ? Results.NotFound() : Results.Ok(ToDto(customer));
        });

        group.MapPost("", async (CreateCustomerRequest req, ICustomerRepository repo, CancellationToken ct) =>
        {
            var customer = new Customer
            {
                Id = req.Id ?? Guid.NewGuid(),
                Name = req.Name,
                Email = req.Email,
                Phone = req.Phone,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await repo.InsertAsync(customer, ct);

            return Results.Created($"/customers/{customer.Id}", ToDto(customer));
        });

        // PUT is "create-or-replace" — idempotent. Existing doc keeps CreatedAt; new docs get it set now.
        group.MapPut("/{id:guid}", async (Guid id, UpdateCustomerRequest req, ICustomerRepository repo, CancellationToken ct) =>
        {
            var existing = await repo.FindByIdAsync(id, ct);
            var now = DateTimeOffset.UtcNow;

            var customer = new Customer
            {
                Id = id,
                Name = req.Name,
                Email = req.Email,
                Phone = req.Phone,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = existing is null ? null : now,
            };

            var replaced = await repo.UpsertAsync(customer, ct);
            return replaced
                ? Results.Ok(ToDto(customer))
                : Results.Created($"/customers/{customer.Id}", ToDto(customer));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ICustomerRepository repo, CancellationToken ct) =>
        {
            var ok = await repo.DeleteAsync(id, ct);

            return ok ? Results.NoContent() : Results.NotFound();
        });

        return endpoints;
    }

    private static CustomerDto ToDto(Customer c) => new(c.Id, c.Name, c.Email, c.Phone);
}
