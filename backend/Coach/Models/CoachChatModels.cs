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
    PlayerReplacementResult? Transfers = null);

[JsonConverter(typeof(JsonStringEnumConverter<CoachRecommendationType>))]
public enum CoachRecommendationType
{
    General,
    Availability,
    Fixture,
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