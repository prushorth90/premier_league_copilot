using Backend.Coach.Models;

namespace Backend.Coach;

public interface ICoachService
{
    Task<CoachChatResponse> ReplyAsync(int teamId, string message, CancellationToken cancellationToken);

    Task<CoachChatResponse> ReplyWithProgressAsync(
        int teamId,
        string message,
        ICoachProgressSink progressSink,
        CancellationToken cancellationToken) =>
        ReplyAsync(teamId, message, cancellationToken);
}