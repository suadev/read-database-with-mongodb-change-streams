namespace Services.ReadModelBuilder.Options;

public class MongoOptions
{
    public const string DefaultSectionName = "mongo";

    public string ConnectionString { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Interval in hours for refreshing resume tokens to prevent oplog expiration.
    /// </summary>
    public int ResumeTokenRefreshIntervalHours { get; set; } = 1;

    /// <summary>
    /// Maximum age in hours before a resume token is considered stale and should be refreshed.
    /// </summary>
    public int MaxResumeTokenAgeHours { get; set; } = 3;

    /// <summary>
    /// Enable periodic resume token refresh to handle long periods of inactivity.
    /// </summary>
    public bool EnableResumeTokenRefresh { get; set; } = true;

    /// <summary>
    /// Maximum number of consecutive failures allowed for the same document before it's skipped as a poison message.
    /// Set to 0 to disable poison message detection (not recommended for production - will retry indefinitely).
    /// Values > 0 enable automatic skip after N failures to prevent pipeline blockage.
    /// Note: Infrastructure errors (network, DB connection) are NOT counted - only data/logic errors count toward this limit.
    /// Default: 15 retries (~5-15 minutes with exponential backoff).
    /// </summary>
    public int MaxConsecutiveFailuresPerDocument { get; set; } = 15;
}
