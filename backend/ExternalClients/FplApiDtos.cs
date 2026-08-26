namespace Backend.ExternalClients;

public sealed record FplBootstrapDto(
    IReadOnlyList<FplEventDto> Events,
    IReadOnlyList<FplTeamDto> Teams,
    IReadOnlyList<FplPlayerTypeDto> ElementTypes,
    IReadOnlyList<FplPlayerDto> Elements);

public sealed record FplEventDto(
    int Id,
    string Name,
    DateTimeOffset DeadlineTime,
    bool Finished,
    bool IsCurrent,
    bool IsNext,
    int AverageEntryScore,
    int? HighestScore);

public sealed record FplTeamDto(
    int Id,
    int Code,
    string Name,
    string ShortName,
    int? Strength,
    int StrengthOverallHome,
    int StrengthOverallAway);

public sealed record FplPlayerTypeDto(
    int Id,
    string SingularName,
    string SingularNameShort,
    int SquadSelect,
    int SquadMinPlay,
    int SquadMaxPlay);

public sealed record FplPlayerDto(
    int Id,
    int Code,
    string FirstName,
    string SecondName,
    string WebName,
    int Team,
    int ElementType,
    int NowCost,
    int TotalPoints,
    int EventPoints,
    string Form,
    string SelectedByPercent,
    decimal ExpectedGoalsPer90,
    decimal ExpectedAssistsPer90,
    string Status,
    string News,
    int? ChanceOfPlayingNextRound);

public sealed record FplFixtureDto(
    int Id,
    int Code,
    int? Event,
    DateTimeOffset? KickoffTime,
    bool Finished,
    bool Started,
    int TeamH,
    int TeamA,
    int? TeamHScore,
    int? TeamAScore,
    int TeamHDifficulty,
    int TeamADifficulty);

public sealed record FplManagerDto(
    int Id,
    string PlayerFirstName,
    string PlayerLastName,
    string Name,
    int StartedEvent,
    int CurrentEvent,
    int SummaryOverallPoints,
    int? SummaryOverallRank,
    int SummaryEventPoints,
    int? SummaryEventRank,
    int LastDeadlineBank,
    int LastDeadlineValue);

public sealed record FplSquadPicksDto(
    string? ActiveChip,
    FplEntryHistoryDto EntryHistory,
    IReadOnlyList<FplPickDto> Picks);

public sealed record FplEntryHistoryDto(
    int Event,
    int Points,
    int TotalPoints,
    int? OverallRank,
    int Bank,
    int Value,
    int EventTransfers,
    int EventTransfersCost,
    int PointsOnBench);

public sealed record FplPickDto(
    int Element,
    int Position,
    int Multiplier,
    bool IsCaptain,
    bool IsViceCaptain,
    int ElementType,
    int? PurchasePrice = null,
    int? SellingPrice = null);

public sealed record FplPlayerSummaryDto(
    IReadOnlyList<FplPlayerFixtureDto> Fixtures,
    IReadOnlyList<FplPlayerGameweekHistoryDto> History,
    IReadOnlyList<FplPlayerSeasonHistoryDto> HistoryPast);

public sealed record FplPlayerFixtureDto(
    int Id,
    int? Event,
    string EventName,
    DateTimeOffset? KickoffTime,
    bool IsHome,
    int TeamH,
    int TeamA,
    int Difficulty);

public sealed record FplPlayerGameweekHistoryDto(
    int Element,
    int Fixture,
    int OpponentTeam,
    int Round,
    bool WasHome,
    DateTimeOffset KickoffTime,
    int TotalPoints,
    int Minutes,
    int GoalsScored,
    int Assists,
    int CleanSheets,
    int GoalsConceded,
    int Bonus,
    int Bps,
    int Value,
    int Selected,
    int TransfersIn,
    int TransfersOut);

public sealed record FplPlayerSeasonHistoryDto(
    string SeasonName,
    int ElementCode,
    int StartCost,
    int EndCost,
    int TotalPoints,
    int Minutes,
    int GoalsScored,
    int Assists,
    int CleanSheets,
    int GoalsConceded,
    int Bonus,
    int Bps);