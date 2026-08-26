using Backend.Coach.Models;

namespace Backend.Coach;

public interface ICoachService
{
    Task<CoachChatResponse> ReplyAsync(int teamId, string message, CancellationToken cancellationToken);
}