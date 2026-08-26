namespace Backend.Coach.Models;

public sealed record FplCoachContext(
    int TeamId,
    string TeamName,
    int Gameweek,
    decimal Bank,
    decimal TeamValue,
    IReadOnlyList<FplCoachSquadPlayer> Squad);

public sealed record FplCoachSquadPlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal Price,
    string Status,
    string News,
    int? ChanceOfPlayingNextRound,
    bool IsStarter,
    bool IsCaptain,
    bool IsViceCaptain);

public sealed record PlayerAvailabilityResult(
    CoachAvailabilityPlayer Player,
    string Status,
    string StatusDescription,
    bool IsAvailable,
    int? ChanceOfPlayingNextRound,
    string? ExpectedReturn,
    decimal Confidence,
    string Evidence,
    string Source);

public sealed record CoachAvailabilityPlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position);

public sealed record PlayerFixtureWindowResult(
    CoachFixturePlayer Player,
    int RequestedGameweeks,
    IReadOnlyList<CoachUpcomingFixture> Fixtures,
    decimal? AverageDifficulty,
    decimal? AggregateScore,
    string ScheduleRating,
    string Explanation,
    string Source);

public sealed record CoachFixturePlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position);

public sealed record CoachUpcomingFixture(
    int FixtureId,
    int Gameweek,
    string GameweekName,
    DateTimeOffset? Kickoff,
    string Opponent,
    bool IsHome,
    string Venue,
    int Difficulty);

public sealed record PlayerReplacementResult(
    CoachTransferPlayer PlayerOut,
    decimal Bank,
    decimal MaximumPurchasePrice,
    int ProjectionGameweeks,
    IReadOnlyList<CoachReplacementCandidate> Candidates,
    string Source);

public sealed record CoachTransferPlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal Price);

public sealed record CoachReplacementCandidate(
    int Rank,
    CoachTransferPlayer Player,
    decimal PriceDifference,
    decimal PlayerOutProjectedPoints,
    decimal CandidateProjectedPoints,
    decimal ProjectedPointDifference,
    decimal Confidence,
    string Reason);

public sealed record PlayerRecommendationResult(
    PlayerRecommendationAction Action,
    decimal ProjectedImpact,
    int ProjectionGameweeks,
    decimal Confidence,
    string Reason,
    CoachReplacementCandidate? RecommendedReplacement,
    PlayerAvailabilityResult Availability,
    PlayerFixtureWindowResult Fixtures,
    PlayerReplacementResult Transfers,
    string Source);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<PlayerRecommendationAction>))]
public enum PlayerRecommendationAction
{
    Hold,
    Bench,
    Transfer
}

public sealed record CoachSpecialistGrounding(
    IReadOnlyList<string> InvokedAgents,
    PlayerAvailabilityResult? Availability,
    PlayerFixtureWindowResult? Fixtures,
    PlayerReplacementResult? Transfers,
    PlayerRecommendationResult? Recommendation);