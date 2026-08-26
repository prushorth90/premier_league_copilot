using Backend.Recommendation.Lineup.Models;

namespace Backend.Recommendation.Lineup;

public interface ILineupRecommendationService
{
    Task<LineupRecommendation> GetRecommendationAsync(int teamId, CancellationToken cancellationToken);
}