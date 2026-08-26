using System.Text.Json.Serialization;

namespace Backend.Coach.Models;

public sealed record CoachChatRequest(int TeamId, string Message);

public sealed record CoachChatResponse(
    string Message,
    int TeamId,
    DateTimeOffset RespondedAt,
    bool IsMocked,
    CoachRecommendationType RecommendationType,
    decimal Confidence,
    CoachPlayerInfo? Player,
    PlayerAvailabilityResult? Availability = null,
    PlayerFixtureWindowResult? Fixtures = null,
    PlayerReplacementResult? Transfers = null,
    PlayerRecommendationResult? Recommendation = null,
    CoachStructuredRecommendation? StructuredRecommendation = null);

[JsonConverter(typeof(JsonStringEnumConverter<CoachRecommendationType>))]
public enum CoachRecommendationType
{
    General,
    Availability,
    Fixture,
    Recommendation,
    Transfer,
    Replacement
}

public sealed record CoachPlayerInfo(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    string Status,
    int? ChanceOfPlayingNextRound,
    string PhotoUrl);

public sealed record CoachStructuredRecommendation(
    CoachPlayerInfo DetectedPlayer,
    PlayerRecommendationAction RecommendedAction,
    decimal Confidence,
    CoachInjuryStatus InjuryStatus,
    CoachFixtureSummary UpcomingFixtureSummary,
    CoachSuggestedReplacement? SuggestedReplacement,
    decimal ProjectedImpact,
    int ProjectionGameweeks,
    string Reason);

public sealed record CoachInjuryStatus(
    string Status,
    string Description,
    bool IsAvailable,
    int? ChanceOfPlayingNextRound,
    string? ExpectedReturn);

public sealed record CoachFixtureSummary(
    int RequestedGameweeks,
    string ScheduleRating,
    decimal? AverageDifficulty,
    decimal? AggregateScore,
    IReadOnlyList<CoachUpcomingFixture> Fixtures);

public sealed record CoachSuggestedReplacement(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal Price,
    decimal PriceDifference,
    decimal ProjectedPointDifference,
    string Reason);