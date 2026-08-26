namespace Backend.Persistence.Entities;

public sealed class RecommendationSnapshot
{
    public Guid Id { get; set; }

    public int FplTeamId { get; set; }

    public required string Kind { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}