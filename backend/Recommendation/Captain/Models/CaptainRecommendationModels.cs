using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Captain.Models;

public sealed record CaptainRecommendation(
    int TeamId,
    int Gameweek,
    DateTimeOffset CalculatedAt,
    CaptainCandidate BestCaptain,
    CaptainCandidate ViceCaptain,
    IReadOnlyList<CaptainCandidate> Alternatives);

public sealed record CaptainCandidate(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal ProjectedPoints,
    decimal RankingScore,
    IReadOnlyList<CaptainFactorBreakdown> Factors,
    string PhotoUrl = PlayerPhotoUrl.Fallback);

public sealed record CaptainFactorBreakdown(
    string Factor,
    decimal Score,
    string Explanation);

public sealed record CaptainCandidateContext(
    Player Player,
    string TeamName,
    string Position,
    PlayerProjection Projection);