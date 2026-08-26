using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public sealed class HomeAwayFactor : IProjectionFactor
{
    public int Order => 50;

    public string Name => "Venue";

    public ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore)
    {
        var contribution = fixture.IsHome ? 0.25m : -0.1m;
        return new(Name, contribution, fixture.IsHome ? "Home fixture bonus." : "Away fixture adjustment.");
    }
}