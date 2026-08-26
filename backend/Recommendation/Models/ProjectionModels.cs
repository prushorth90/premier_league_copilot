using Backend.Models;

namespace Backend.Recommendation.Models;

public sealed record PlayerProjection(
    int PlayerId,
    string PlayerName,
    DateTimeOffset CalculatedAt,
    decimal ExpectedMinutes,
    IReadOnlyList<ProjectionHorizon> Horizons);

public sealed record ProjectionHorizon(
    int Gameweeks,
    decimal ProjectedPoints,
    IReadOnlyList<ProjectionFactorBreakdown> Factors,
    IReadOnlyList<FixtureProjection> Fixtures);

public sealed record ProjectionFactorBreakdown(
    string Factor,
    decimal Contribution,
    string Explanation);

public sealed record FixtureProjection(
    int FixtureId,
    int? Gameweek,
    decimal ProjectedPoints,
    IReadOnlyList<ProjectionFactorBreakdown> Factors);

public sealed record ProjectionContext(
    Player Player,
    PlayerHistory History,
    decimal ExpectedMinutes,
    decimal HistoricalPointsPer90);