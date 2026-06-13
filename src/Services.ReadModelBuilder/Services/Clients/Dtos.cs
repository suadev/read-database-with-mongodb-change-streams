namespace Services.ReadModelBuilder.Services.Clients;

public record CustomerDto(Guid Id, string Name, string Email, string Phone);

public record ProductDto(Guid Id, string Name, string Sku, decimal Price);
