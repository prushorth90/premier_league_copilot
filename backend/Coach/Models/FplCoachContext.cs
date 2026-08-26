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