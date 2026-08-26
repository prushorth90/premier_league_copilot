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
    int? ChanceOfPlayingNextRound,
    bool IsStarter,
    bool IsCaptain,
    bool IsViceCaptain);