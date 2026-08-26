using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain.Factors;

public sealed class ProjectedPointsCaptainFactor : ICaptainFactor
{
    private const decimal Weight = 0.65m;

    public int Order => 10;

    public string Name => "Projected points";

    public CaptainFactorBreakdown Calculate(CaptainCandidateContext context)
    {
        var projectedPoints = context.Projection.Horizons.Single(item => item.Gameweeks == 1).ProjectedPoints;
        return new(Name, projectedPoints * Weight, $"{projectedPoints:0.00} projected points × {Weight:0.00}.");
    }
}