using Backend.Recommendation.Models;

namespace Backend.Recommendation;

public interface IProjectedPointsService
{
    Task<PlayerProjection> GetPlayerProjectionAsync(int playerId, CancellationToken cancellationToken);
}