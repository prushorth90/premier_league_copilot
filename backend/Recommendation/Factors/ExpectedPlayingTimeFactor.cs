using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class ExpectedPlayingTimeFactor : IProjectionFactor
{
    private const decimal FullMatchContribution = 1.5m;

    public int Order => 30;

    public string Name => "Expected playing time";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var contribution = context.ExpectedMinutes / 90m * FullMatchContribution;
        return new(Name, contribution, $"Expected {context.ExpectedMinutes:0} minutes from recent appearances.");
    }
}