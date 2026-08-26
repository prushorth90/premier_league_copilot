using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Transfer.Models;

public sealed record TransferRecommendationResponse(
    int TeamId,
    int Gameweek,
    DateTimeOffset CalculatedAt,
    decimal Bank,
    IReadOnlyList<TransferRecommendation> Recommendations,
    IReadOnlyList<TransferCombinationRecommendation> Combinations,
    decimal? SelectedPlayerSellingPrice = null);

public sealed record TransferCombinationRecommendation(
    IReadOnlyList<TransferRecommendation> Transfers,
    decimal TotalPriceDifference,
    IReadOnlyList<TransferHorizonGain> ExpectedPointGains,
    decimal WeightedGain,
    decimal ConfidenceScore,
    IReadOnlyList<TransferExplanation> Explanations);

public sealed record TransferRecommendation(
    TransferPlayer PlayerOut,
    TransferPlayer PlayerIn,
    decimal PriceDifference,
    IReadOnlyList<TransferHorizonGain> ExpectedPointGains,
    decimal WeightedGain,
    decimal ConfidenceScore,
    IReadOnlyList<TransferExplanation> Explanations);

public sealed record TransferPlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal Price,
    string Status,
    decimal ExpectedMinutes,
    IReadOnlyList<string> NextFixtures,
    string PhotoUrl = PlayerPhotoUrl.Fallback);

public sealed record TransferHorizonGain(
    int Gameweeks,
    decimal PlayerOutPoints,
    decimal PlayerInPoints,
    decimal ExpectedPointGain);

public sealed record TransferExplanation(
    string Factor,
    decimal Score,
    string Explanation);

public sealed record TransferPlayerContext(
    Player Player,
    string TeamName,
    string Position,
    PlayerProjection Projection,
    int? SellingPrice = null,
    IReadOnlyList<string>? NextFixtures = null);