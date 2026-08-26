using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Lineup.Models;

public sealed record LineupRecommendation(
    int TeamId,
    int Gameweek,
    DateTimeOffset CalculatedAt,
    string Formation,
    IReadOnlyList<LineupPlayer> StartingXi,
    IReadOnlyList<LineupPlayer> Bench,
    IReadOnlyList<LineupChange> Changes);

public sealed record LineupPlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal ProjectedPoints,
    decimal ExpectedMinutes,
    decimal RankingScore,
    int CurrentSquadPosition,
    int RecommendedSquadPosition,
    IReadOnlyList<LineupHorizonProjection> Projections);

public sealed record LineupHorizonProjection(
    int Gameweeks,
    decimal ProjectedPoints);

public sealed record LineupChange(
    int PlayerId,
    string PlayerName,
    string ChangeType,
    int CurrentSquadPosition,
    int RecommendedSquadPosition);

public sealed record LineupCandidateContext(
    Player Player,
    string TeamName,
    string Position,
    int CurrentSquadPosition,
    PlayerProjection Projection);