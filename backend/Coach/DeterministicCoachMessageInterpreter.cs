using Backend.Coach.Models;

namespace Backend.Coach;

public sealed class DeterministicCoachMessageInterpreter : ICoachMessageInterpreter
{
    public Task<CoachMessageInterpretation> InterpretAsync(
        FplCoachContext context,
        int? playerId,
        string message,
        CancellationToken cancellationToken)
    {
        var type = GetRecommendationType(message);
        var fixtureGameweeks = GetRequestedGameweeks(message);
        var decisionGameweeks = fixtureGameweeks == 3
            && !message.Contains("3", StringComparison.OrdinalIgnoreCase)
                ? 5
                : fixtureGameweeks;
        return Task.FromResult(new CoachMessageInterpretation(type, fixtureGameweeks, decisionGameweeks, 3));
    }

    private static CoachRecommendationType GetRecommendationType(string message)
    {
        if (ContainsAny(message, "injur", "doubt", "available")) return CoachRecommendationType.Availability;
        if (ContainsAny(message, "fixture", "schedule", "opponent")) return CoachRecommendationType.Fixture;
        if (ContainsAny(message, "bench", "hold", "start", "what should")) return CoachRecommendationType.Recommendation;
        if (ContainsAny(message, "sell", "transfer out")) return CoachRecommendationType.Transfer;
        if (ContainsAny(message, "replace", "who")) return CoachRecommendationType.Replacement;
        return CoachRecommendationType.General;
    }

    private static int GetRequestedGameweeks(string message)
    {
        for (var gameweeks = 1; gameweeks <= 5; gameweeks++)
        {
            if (ContainsAny(message, $"{gameweeks} fixture", $"{gameweeks} gameweek", $"next {gameweeks}"))
            {
                return gameweeks;
            }
        }

        return 3;
    }

    private static bool ContainsAny(string message, params string[] values) =>
        values.Any(value => message.Contains(value, StringComparison.OrdinalIgnoreCase));
}