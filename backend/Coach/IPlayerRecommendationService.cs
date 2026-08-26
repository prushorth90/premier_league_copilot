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

    Task<PlayerRecommendationResult?> GetRecommendationIfAtRiskAsync(
        FplCoachContext context,
        PlayerAvailabilityResult verifiedAvailability,
        int gameweeks,
        int candidateLimit,
        CancellationToken cancellationToken);
}