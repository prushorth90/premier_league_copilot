using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class PositionFactor : IProjectionFactor
{
    public int Order => 10;

    public string Name => "Position";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var contribution = context.Player.PositionId switch
        {
            1 => 1.8m,
            2 => 1.7m,
            3 => 1.5m,
            4 => 1.4m,
            _ => 1.5m
        };

        return new(Name, contribution, $"Position {context.Player.PositionId} baseline.");
    }
}