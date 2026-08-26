using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain.Factors;

public sealed class AttackingPotentialCaptainFactor : ICaptainFactor
{
    public int Order => 40;

    public string Name => "Attacking potential";

    public CaptainFactorBreakdown Calculate(CaptainCandidateContext context)
    {
        var positionMultiplier = context.Player.PositionId switch
        {
            1 => 0.15m,
            2 => 0.5m,
            3 => 1m,
            4 => 1.1m,
            _ => 0.75m
        };
        var score = (context.Player.ExpectedGoalsPer90 * 4m + context.Player.ExpectedAssistsPer90 * 3m) * positionMultiplier;
        return new(
            Name,
            score,
            $"xG/90 {context.Player.ExpectedGoalsPer90:0.00}, xA/90 {context.Player.ExpectedAssistsPer90:0.00}, position multiplier {positionMultiplier:0.00}.");
    }
}