using System.Net.Http.Json;

namespace Services.Orders.Services.Clients;

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;

    public ProductServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyDictionary<Guid, ProductPriceDto>> GetPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds == null || productIds.Count == 0)
        {
            return new Dictionary<Guid, ProductPriceDto>();
        }

        var idsParam = string.Join(',', productIds.Distinct());
        var response = await _httpClient.GetAsync($"/products/batch?ids={Uri.EscapeDataString(idsParam)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<ProductPriceDto>>(cancellationToken: cancellationToken)
            ?? [];
        return dtos.ToDictionary(p => p.Id);
    }
}
