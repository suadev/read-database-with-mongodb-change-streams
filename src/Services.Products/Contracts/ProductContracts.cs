namespace Services.Products.Contracts;

public record ProductDto(Guid Id, string Name, string Sku, decimal Price);

public record CreateProductRequest(Guid? Id, string Name, string Sku, decimal Price);

public record UpdateProductRequest(string Name, string Sku, decimal Price);
