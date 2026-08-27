using Backend.Coach.Models;

namespace Backend.Coach;

public interface ICoachMessageInterpreter
{
    Task<CoachMessageInterpretation> InterpretAsync(
        FplCoachContext context,
        int? playerId,
        string message,
        CancellationToken cancellationToken);
}

public sealed record CoachMessageInterpretation(
    CoachRecommendationType RecommendationType,
    int FixtureGameweeks,
    int DecisionGameweeks,
    int CandidateLimit);