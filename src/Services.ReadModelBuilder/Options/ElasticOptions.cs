namespace Services.ReadModelBuilder.Options;

public class ElasticOptions
{
    public const string DefaultSectionName = "elastic";

    public string Url { get; set; } = "http://localhost:9200";

    public string OrderDetailsIndexName { get; set; } = Domain.Constants.ElasticConstants.OrderDetailsIndexName;
}
