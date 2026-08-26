using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain.Factors;

public sealed class FixtureQualityCaptainFactor : ICaptainFactor
{
    public int Order => 30;

    public string Name => "Fixture quality";

    public CaptainFactorBreakdown Calculate(CaptainCandidateContext context)
    {
        var horizon = context.Projection.Horizons.Single(item => item.Gameweeks == 1);
        var score = horizon.Factors
            .Where(item => item.Factor is "Fixture difficulty" or "Venue")
            .Sum(item => item.Contribution);
        return new(Name, score, "Fixture-difficulty and home/away contributions from the projection model.");
    }
}