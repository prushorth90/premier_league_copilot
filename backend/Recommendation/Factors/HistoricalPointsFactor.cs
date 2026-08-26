using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class HistoricalPointsFactor : IProjectionFactor
{
    private const decimal Weight = 0.25m;

    public int Order => 60;

    public string Name => "Historical FPL points";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var contribution = context.HistoricalPointsPer90 * Weight;
        return new(Name, contribution, $"Historical {context.HistoricalPointsPer90:0.00} points per 90 × {Weight:0.00}.");
    }
}