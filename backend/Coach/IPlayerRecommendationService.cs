using Backend.Coach.Models;

namespace Backend.Coach;

public interface IPlayerRecommendationService
{
    Task<PlayerRecommendationResult> GetRecommendationAsync(
        FplCoachContext context,
        int playerId,
        int gameweeks,
        int candidateLimit,
        CancellationToken cancellationToken);
}