using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain.Factors;

public sealed class ExpectedMinutesCaptainFactor : ICaptainFactor
{
    public int Order => 20;

    public string Name => "Expected minutes";

    public CaptainFactorBreakdown Calculate(CaptainCandidateContext context)
    {
        var score = context.Projection.ExpectedMinutes / 90m * 1.5m;
        return new(Name, score, $"Expected {context.Projection.ExpectedMinutes:0} minutes.");
    }
}