using Backend.Coach.Models;

namespace Backend.Coach;

public interface IFplCoachOrchestrator
{
    Task<FplCoachOrchestrationResult> OrchestrateAsync(
        FplCoachContext context,
        int? playerId,
        string message,
        ICoachProgressSink? progressSink,
        CancellationToken cancellationToken);
}

public sealed record FplCoachOrchestrationResult(
    CoachRecommendationType RecommendationType,
    decimal Confidence,
    PlayerAvailabilityResult? Availability,
    PlayerFixtureWindowResult? Fixtures,
    PlayerReplacementResult? Transfers,
    PlayerRecommendationResult? Recommendation,
    CoachSpecialistGrounding Grounding);