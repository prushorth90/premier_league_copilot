using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class AvailabilityFactor : IProjectionFactor
{
    public int Order => 100;

    public string Name => "Availability";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var multiplier = GetMultiplier(context.Player);
        var contribution = currentScore * (multiplier - 1m);
        return new(Name, contribution, $"Status '{context.Player.Status}' applies a {multiplier:P0} availability multiplier.");
    }

    private static decimal GetMultiplier(Player player) => player.Status switch
    {
        "a" => 1m,
        "d" => Math.Clamp((player.ChanceOfPlayingNextRound ?? 50) / 100m, 0m, 1m),
        "i" or "s" or "u" => 0m,
        _ => 0.75m
    };
}