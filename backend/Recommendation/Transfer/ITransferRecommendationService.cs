using Backend.Recommendation.Transfer.Models;

namespace Backend.Recommendation.Transfer;

public interface ITransferRecommendationService
{
    Task<TransferRecommendationResponse> GetRecommendationsAsync(
        int teamId,
        int limit,
        CancellationToken cancellationToken);

    Task<TransferRecommendationResponse> GetReplacementRecommendationsAsync(
        int teamId,
        int playerOutId,
        int limit,
        CancellationToken cancellationToken);
}