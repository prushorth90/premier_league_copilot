using Backend.Coach.Models;

namespace Backend.Coach;

public static class PlayerRecommendationPolicy
{
    private const decimal MinimumTransferGain = 3m;

    public static PlayerRecommendationResult Evaluate(
        PlayerAvailabilityResult availability,
        PlayerFixtureWindowResult fixtures,
        PlayerReplacementResult transfers)
    {
        ValidateMatchingPlayers(availability, fixtures, transfers);
        var bestReplacement = transfers.Candidates.FirstOrDefault();
        var projectedImpact = bestReplacement?.ProjectedPointDifference ?? 0m;
        var transferIsWorthwhile = bestReplacement is not null && projectedImpact >= MinimumTransferGain;
        var unavailable = availability.Status is "i" or "s" or "u" or "n";
        var doubtful = availability.Status == "d"
            || availability.ChanceOfPlayingNextRound is int chance && chance < 75;
        var difficultFixture = fixtures.ScheduleRating == "Difficult";

        var action = transferIsWorthwhile && (unavailable || doubtful || difficultFixture || projectedImpact >= 5m)
            ? PlayerRecommendationAction.Transfer
            : unavailable || doubtful || difficultFixture
                ? PlayerRecommendationAction.Bench
                : PlayerRecommendationAction.Hold;
        var confidence = CalculateConfidence(action, availability, fixtures, bestReplacement);
        var reason = action switch
        {
            PlayerRecommendationAction.Transfer => $"Transfer to {bestReplacement!.Player.PlayerName}: the legal replacement projects {projectedImpact:+0.00;-0.00;0.00} points better over {transfers.ProjectionGameweeks} gameweeks.",
            PlayerRecommendationAction.Bench when unavailable => $"Bench {availability.Player.PlayerName}: verified status is {availability.StatusDescription}, and no legal replacement clears the {MinimumTransferGain:0.00}-point transfer threshold.",
            PlayerRecommendationAction.Bench when doubtful => $"Bench {availability.Player.PlayerName}: availability is uncertain, and no legal replacement clears the {MinimumTransferGain:0.00}-point transfer threshold.",
            PlayerRecommendationAction.Bench => $"Bench {availability.Player.PlayerName}: the upcoming schedule is difficult, and no legal replacement clears the {MinimumTransferGain:0.00}-point transfer threshold.",
            _ => $"Hold {availability.Player.PlayerName}: availability and fixtures do not justify a transfer, and no legal candidate provides a sufficient projected gain."
        };

        return new PlayerRecommendationResult(
            action,
            action == PlayerRecommendationAction.Transfer ? projectedImpact : 0m,
            transfers.ProjectionGameweeks,
            confidence,
            reason,
            action == PlayerRecommendationAction.Transfer ? bestReplacement : null,
            availability,
            fixtures,
            transfers,
            "Deterministic C# recommendation policy using verified availability, fixtures, projections, budget, position, ownership, and club constraints");
    }

    private static decimal CalculateConfidence(
        PlayerRecommendationAction action,
        PlayerAvailabilityResult availability,
        PlayerFixtureWindowResult fixtures,
        CoachReplacementCandidate? replacement)
    {
        var fixtureConfidence = fixtures.Fixtures.Count == 0 ? 50m : 95m;
        var transferConfidence = action == PlayerRecommendationAction.Transfer
            ? replacement!.Confidence
            : replacement?.Confidence ?? 80m;
        return Math.Round(
            Math.Clamp(availability.Confidence * 0.4m + fixtureConfidence * 0.25m + transferConfidence * 0.35m, 0m, 100m),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static void ValidateMatchingPlayers(
        PlayerAvailabilityResult availability,
        PlayerFixtureWindowResult fixtures,
        PlayerReplacementResult transfers)
    {
        var playerId = availability.Player.PlayerId;
        if (fixtures.Player.PlayerId != playerId || transfers.PlayerOut.PlayerId != playerId)
        {
            throw new InvalidOperationException("Recommendation inputs must describe the same owned player.");
        }
    }
}