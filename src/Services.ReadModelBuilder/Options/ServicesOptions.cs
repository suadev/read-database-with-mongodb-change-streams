namespace Services.ReadModelBuilder.Options;

public class ServicesOptions
{
    public const string DefaultSectionName = "services";

    public ServiceEndpointOptions Customer { get; set; } = new() { BaseUrl = "http://localhost:5101" };

    public ServiceEndpointOptions Product { get; set; } = new() { BaseUrl = "http://localhost:5102" };
}

public class ServiceEndpointOptions
{
    public string BaseUrl { get; set; }
}
