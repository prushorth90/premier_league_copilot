using Backend.Recommendation.Transfer.Models;

namespace Backend.Recommendation.Transfer;

public interface ITransferRecommendationEngine
{
    IReadOnlyList<TransferRecommendation> Rank(
        IReadOnlyList<TransferPlayerContext> squad,
        IReadOnlyList<TransferPlayerContext> market,
        int bank,
        int limit = 20);
}