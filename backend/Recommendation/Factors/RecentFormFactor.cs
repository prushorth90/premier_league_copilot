using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class RecentFormFactor : IProjectionFactor
{
    private const decimal Weight = 0.35m;

    public int Order => 20;

    public string Name => "Recent form";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var contribution = context.Player.Form * Weight;
        return new(Name, contribution, $"Form {context.Player.Form:0.0} × {Weight:0.00}.");
    }
}