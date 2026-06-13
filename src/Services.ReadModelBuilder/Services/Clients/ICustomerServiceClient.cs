namespace Services.ReadModelBuilder.Services.Clients;

public interface ICustomerServiceClient
{
    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
