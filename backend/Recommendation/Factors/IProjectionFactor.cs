using Backend.Models;
using Backend.Recommendation.Models;

namespace Backend.Recommendation.Factors;

public interface IProjectionFactor
{
    int Order { get; }

    string Name { get; }

    ProjectionFactorBreakdown Calculate(
        ProjectionContext context,
        PlayerFixture fixture,
        decimal currentScore);
}