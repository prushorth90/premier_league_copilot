using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation;

public interface IProjectedPointsCalculator
{
    PlayerProjection Calculate(Player player, PlayerHistory history);
}