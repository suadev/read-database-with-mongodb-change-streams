namespace Services.ReadModelBuilder.Services.Clients;

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;

    public ProductServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return Array.Empty<ProductDto>();
        }

        var idsParam = string.Join(',', ids);
        var response = await _httpClient.GetAsync($"/products/batch?ids={Uri.EscapeDataString(idsParam)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<ProductDto>>(cancellationToken: cancellationToken);
        return dtos ?? new List<ProductDto>();
    }
}
