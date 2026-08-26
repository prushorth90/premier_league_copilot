namespace Backend.Persistence.Entities;

public sealed class RecommendationHistoryEntry
{
    public long Id { get; set; }

    public int FplTeamId { get; set; }

    public required string Kind { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}