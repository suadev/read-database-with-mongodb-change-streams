namespace Services.ReadModelBuilder.Options;

public class SnapshotOptions
{
    public const string DefaultSectionName = "snapshot";

    public int BatchSize { get; set; } = 100;
}
