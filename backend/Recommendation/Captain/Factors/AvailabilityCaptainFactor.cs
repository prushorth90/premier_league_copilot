using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain.Factors;

public sealed class AvailabilityCaptainFactor : ICaptainFactor
{
    public int Order => 50;

    public string Name => "Availability";

    public CaptainFactorBreakdown Calculate(CaptainCandidateContext context)
    {
        var score = context.Player.Status switch
        {
            "a" => 0m,
            "d" => -2m * (1m - (context.Player.ChanceOfPlayingNextRound ?? 50) / 100m),
            "i" or "s" or "u" => -10m,
            _ => -1m
        };
        return new(Name, score, $"Availability status '{context.Player.Status}'.");
    }
}