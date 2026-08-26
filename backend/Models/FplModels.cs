namespace Backend.Models;

public sealed record BootstrapData(
    IReadOnlyList<Gameweek> Gameweeks,
    IReadOnlyList<Team> Teams,
    IReadOnlyList<PlayerPosition> PlayerPositions,
    IReadOnlyList<Player> Players);

public sealed record Gameweek(
    int Id,
    string Name,
    DateTimeOffset Deadline,
    bool Finished,
    bool IsCurrent,
    bool IsNext,
    int AverageScore,
    int? HighestScore);

public sealed record Team(int Id, int Code, string Name, string ShortName, int? Strength, int HomeStrength, int AwayStrength);

public sealed record PlayerPosition(int Id, string Name, string ShortName, int SquadSize, int MinimumStarters, int MaximumStarters);

public sealed record Player(
    int Id,
    int Code,
    string FirstName,
    string LastName,
    string DisplayName,
    int TeamId,
    int PositionId,
    int Price,
    int TotalPoints,
    int GameweekPoints,
    decimal Form,
    decimal OwnershipPercentage,
    string Status,
    string News,
    int? ChanceOfPlayingNextRound);

public sealed record Fixture(
    int Id,
    int Code,
    int? Gameweek,
    DateTimeOffset? Kickoff,
    bool Finished,
    bool Started,
    int HomeTeamId,
    int AwayTeamId,
    int? HomeScore,
    int? AwayScore,
    int HomeDifficulty,
    int AwayDifficulty);

public sealed record Manager(
    int Id,
    string FirstName,
    string LastName,
    string TeamName,
    int StartedGameweek,
    int CurrentGameweek,
    int OverallPoints,
    int? OverallRank,
    int GameweekPoints,
    int? GameweekRank,
    int Bank,
    int TeamValue);

public sealed record Squad(
    string? ActiveChip,
    SquadGameweekSummary Summary,
    IReadOnlyList<SquadPick> Picks);

public sealed record SquadGameweekSummary(
    int Gameweek,
    int Points,
    int TotalPoints,
    int? OverallRank,
    int Bank,
    int TeamValue,
    int Transfers,
    int TransferCost,
    int BenchPoints);

public sealed record SquadPick(int PlayerId, int Position, int Multiplier, bool IsCaptain, bool IsViceCaptain, int PositionId);

public sealed record PlayerHistory(
    IReadOnlyList<PlayerFixture> Fixtures,
    IReadOnlyList<PlayerGameweekHistory> CurrentSeason,
    IReadOnlyList<PlayerSeasonHistory> PreviousSeasons);

public sealed record PlayerFixture(int Id, int? Gameweek, string GameweekName, DateTimeOffset? Kickoff, bool IsHome, int HomeTeamId, int AwayTeamId, int Difficulty);

public sealed record PlayerGameweekHistory(
    int PlayerId,
    int FixtureId,
    int OpponentTeamId,
    int Gameweek,
    bool WasHome,
    DateTimeOffset Kickoff,
    int Points,
    int Minutes,
    int Goals,
    int Assists,
    int CleanSheets,
    int GoalsConceded,
    int Bonus,
    int Bps,
    int Price,
    int Selected,
    int TransfersIn,
    int TransfersOut);

public sealed record PlayerSeasonHistory(
    string Season,
    int PlayerCode,
    int StartPrice,
    int EndPrice,
    int Points,
    int Minutes,
    int Goals,
    int Assists,
    int CleanSheets,
    int GoalsConceded,
    int Bonus,
    int Bps);