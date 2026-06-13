namespace Services.ReadModelBuilder.Services.Clients;

public class CustomerServiceClient : ICustomerServiceClient
{
    private readonly HttpClient _httpClient;

    public CustomerServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return Array.Empty<CustomerDto>();
        }

        var idsParam = string.Join(',', ids);
        var response = await _httpClient.GetAsync($"/customers/batch?ids={Uri.EscapeDataString(idsParam)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<CustomerDto>>(cancellationToken: cancellationToken);
        return dtos ?? new List<CustomerDto>();
    }
}
