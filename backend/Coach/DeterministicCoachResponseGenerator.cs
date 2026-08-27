using Backend.Coach.Models;

namespace Backend.Coach;

public sealed class DeterministicCoachResponseGenerator : ICoachResponseGenerator
{
    public Task<string> GenerateAsync(CoachResponseContext context, CancellationToken cancellationToken)
    {
        var orchestration = context.Orchestration;
        var recommendation = orchestration.Recommendation;
        if (recommendation is not null)
        {
            var deterministic = $"Deterministic recommendation: {recommendation.Action.ToString().ToUpperInvariant()}. "
                + $"{recommendation.Reason} Confidence: {recommendation.Confidence:0}%. "
                + $"Projected impact: {recommendation.ProjectedImpact:+0.00;-0.00;0.00} points over {recommendation.ProjectionGameweeks} gameweeks.";
            var availability = orchestration.Availability;
            return Task.FromResult(orchestration.RecommendationType == CoachRecommendationType.Availability
                && availability is not null
                && availability.Status != "i"
                    ? $"Official FPL data does not confirm that {availability.Player.PlayerName} is injured. Current status: {availability.StatusDescription}. {deterministic}"
                    : deterministic);
        }

        if (orchestration.RecommendationType == CoachRecommendationType.Availability
            && orchestration.Availability is PlayerAvailabilityResult availabilityResult)
        {
            var chance = availabilityResult.ChanceOfPlayingNextRound is int value ? $" Chance of playing: {value}%." : string.Empty;
            var expectedReturn = availabilityResult.ExpectedReturn is not null ? $" Expected return: {availabilityResult.ExpectedReturn}." : string.Empty;
            return Task.FromResult(availabilityResult.Status == "i"
                ? $"Official FPL data confirms that {availabilityResult.Player.PlayerName} is injured.{chance}{expectedReturn} Confidence: {availabilityResult.Confidence:0}%."
                : $"Official FPL data does not confirm that {availabilityResult.Player.PlayerName} is injured. Current status: {availabilityResult.StatusDescription}.{chance}{expectedReturn} Confidence: {availabilityResult.Confidence:0}%.");
        }

        if (orchestration.Fixtures is not null)
        {
            return Task.FromResult(orchestration.Fixtures.Explanation);
        }

        return Task.FromResult("I could not identify a supported injury, fixture, or transfer question for a player in the connected squad.");
    }
}