using Backend.Models;

namespace Backend.DTOs;

public sealed record FplTeamResponse(
    int Id,
    string ManagerName,
    string TeamName,
    int StartedGameweek,
    int CurrentGameweek,
    int OverallPoints,
    int? OverallRank,
    int GameweekPoints,
    int? GameweekRank,
    decimal Bank,
    decimal TeamValue,
    int? FreeTransfers,
    FplGameweekResponse? NextGameweek);

public sealed record FplGameweekResponse(
    int Id,
    string Name,
    DateTimeOffset Deadline);

public sealed record FplPlayerResponse(
    int Id,
    int Code,
    string FirstName,
    string LastName,
    string DisplayName,
    int TeamId,
    string TeamName,
    int PositionId,
    string Position,
    decimal Price,
    int TotalPoints,
    int GameweekPoints,
    decimal Form,
    decimal OwnershipPercentage,
    string Status,
    string News,
    int? ChanceOfPlayingNextRound,
    string? UpcomingFixture,
    string PhotoUrl = PlayerPhotoUrl.Fallback);

public sealed record FplFixtureResponse(
    int Id,
    int Code,
    int? Gameweek,
    DateTimeOffset? Kickoff,
    bool Finished,
    bool Started,
    int HomeTeamId,
    string HomeTeam,
    int AwayTeamId,
    string AwayTeam,
    int? HomeScore,
    int? AwayScore,
    int HomeDifficulty,
    int AwayDifficulty);

public sealed record FplSquadResponse(
    int TeamId,
    string TeamName,
    int Gameweek,
    string? ActiveChip,
    FplSquadSummaryResponse Summary,
    IReadOnlyList<FplSquadPickResponse> Picks);

public sealed record FplSquadSummaryResponse(
    int Points,
    int TotalPoints,
    int? OverallRank,
    decimal Bank,
    decimal TeamValue,
    int Transfers,
    int TransferCost,
    int BenchPoints);

public sealed record FplSquadPickResponse(
    int PlayerId,
    string DisplayName,
    string TeamName,
    string PositionName,
    decimal Price,
    int SquadPosition,
    int Multiplier,
    bool IsCaptain,
    bool IsViceCaptain,
    int GameweekPoints,
    string? NextOpponent,
    string PhotoUrl = PlayerPhotoUrl.Fallback);