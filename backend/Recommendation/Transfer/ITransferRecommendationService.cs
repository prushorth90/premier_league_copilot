using Backend.Recommendation.Transfer.Models;

namespace Backend.Recommendation.Transfer;

public interface ITransferRecommendationService
{
    Task<TransferRecommendationResponse> GetRecommendationsAsync(
        int teamId,
        int limit,
        CancellationToken cancellationToken);
}