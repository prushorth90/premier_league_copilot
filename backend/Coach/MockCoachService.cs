using Backend.Coach.Models;

namespace Backend.Coach;

public sealed class MockCoachService(TimeProvider timeProvider) : ICoachService
{
    public Task<CoachChatResponse> ReplyAsync(
        int teamId,
        string message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedMessage = message.Trim();
        var reply = CreateReply(normalizedMessage);

        return Task.FromResult(new CoachChatResponse(
            reply,
            teamId,
            timeProvider.GetUtcNow(),
            true));
    }

    private static string CreateReply(string message)
    {
        if (message.Contains("injur", StringComparison.OrdinalIgnoreCase))
        {
            return "I have noted the injury concern. Check the player's latest availability and expected minutes before changing your lineup or making a transfer.";
        }

        if (message.Contains("sell", StringComparison.OrdinalIgnoreCase))
        {
            return "Compare the player's 3 and 5 gameweek projection with your best valid replacement before selling. The Transfers page already accounts for budget, position, and club limits.";
        }

        if (message.Contains("replace", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("who", StringComparison.OrdinalIgnoreCase))
        {
            return "Start with the highest-ranked same-position options on the Transfers page. A future AI version will discuss those live recommendations directly in this chat.";
        }

        return "I can discuss injuries, captaincy, lineup choices, and transfers. This first version uses a mocked response while the AI reasoning layer is being prepared.";
    }
}