using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain;

public interface ICaptainRecommendationService
{
    Task<CaptainRecommendation> GetRecommendationAsync(int teamId, CancellationToken cancellationToken);
}