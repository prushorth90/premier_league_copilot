namespace Backend.Configuration;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public int RecommendationSnapshotMinutes { get; init; } = 15;

    public bool ApplyMigrations { get; init; } = true;
}