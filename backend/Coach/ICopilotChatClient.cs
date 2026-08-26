using Backend.Coach.Models;

namespace Backend.Coach;

public interface ICopilotChatClient
{
    Task<string> GenerateAsync(
        string message,
        FplCoachContext context,
        CoachSpecialistGrounding grounding,
        CancellationToken cancellationToken);
}