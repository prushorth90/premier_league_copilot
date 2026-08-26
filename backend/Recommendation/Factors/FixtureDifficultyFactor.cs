using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class FixtureDifficultyFactor : IProjectionFactor
{
    private const decimal Weight = 0.4m;

    public int Order => 40;

    public string Name => "Fixture difficulty";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var contribution = (3 - fixture.Difficulty) * Weight;
        return new(Name, contribution, $"FDR {fixture.Difficulty}; easier fixtures add points and harder fixtures subtract them.");
    }
}