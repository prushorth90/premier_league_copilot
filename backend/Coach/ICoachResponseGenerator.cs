using Backend.Coach.Models;

namespace Backend.Coach;

public interface ICoachResponseGenerator
{
    Task<string> GenerateAsync(CoachResponseContext context, CancellationToken cancellationToken);
}

public sealed record CoachResponseContext(
    string UserMessage,
    FplCoachContext SquadContext,
    CoachPlayerInfo? DetectedPlayer,
    FplCoachOrchestrationResult Orchestration);