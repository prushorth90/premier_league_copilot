namespace Backend.Configuration;

public sealed class FplApiOptions
{
    public const string SectionName = "FplApi";

    public string BaseUrl { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 15;

    public int BootstrapCacheMinutes { get; init; } = 60;

    public int FixturesCacheMinutes { get; init; } = 15;

    public int ManagerCacheMinutes { get; init; } = 5;

    public int SquadCacheMinutes { get; init; } = 5;

    public int PlayerHistoryCacheMinutes { get; init; } = 30;
}