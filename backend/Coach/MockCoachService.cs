using Backend.Coach.Models;
using Backend.Models;
using Backend.Services;

namespace Backend.Coach;

public sealed class MockCoachService(
    IFplDataService fplDataService,
    TimeProvider timeProvider) : ICoachService
{
    public async Task<CoachChatResponse> ReplyAsync(
        int teamId,
        string message,
        CancellationToken cancellationToken)
    {
        var normalizedMessage = message.Trim();
        var managerTask = fplDataService.GetManagerAsync(teamId, cancellationToken);
        var bootstrapTask = fplDataService.GetBootstrapDataAsync(cancellationToken);
        await Task.WhenAll(managerTask, bootstrapTask);
        var manager = await managerTask;
        var bootstrap = await bootstrapTask;
        var squad = await fplDataService.GetManagerPicksAsync(teamId, manager.CurrentGameweek, cancellationToken);
        var squadPlayerIds = squad.Picks.Select(pick => pick.PlayerId).ToHashSet();
        var matchedPlayer = bootstrap.Players
            .Where(player => squadPlayerIds.Contains(player.Id))
            .OrderByDescending(player => player.DisplayName.Length)
            .FirstOrDefault(player => IsPlayerMentioned(normalizedMessage, player));
        var recommendationType = GetRecommendationType(normalizedMessage);
        var confidence = GetConfidence(recommendationType, matchedPlayer);
        var playerInfo = matchedPlayer is null
            ? null
            : MapPlayer(matchedPlayer, bootstrap);
        var reply = CreateReply(normalizedMessage, recommendationType, playerInfo);

        return new CoachChatResponse(
            reply,
            teamId,
            timeProvider.GetUtcNow(),
            true,
            recommendationType,
            confidence,
            playerInfo);
    }

    private static string CreateReply(
        string message,
        CoachRecommendationType recommendationType,
        CoachPlayerInfo? player)
    {
        var playerName = player?.PlayerName ?? "that player";
        if (recommendationType == CoachRecommendationType.Availability)
        {
            return $"I found {playerName} in your current squad and noted the injury concern. Check the latest availability and expected minutes before changing your lineup or making a transfer.";
        }

        if (recommendationType == CoachRecommendationType.Transfer)
        {
            return $"Before selling {playerName}, compare the 3 and 5 gameweek projection with the best valid replacement. The Transfers page already accounts for budget, position, and club limits.";
        }

        if (recommendationType == CoachRecommendationType.Replacement)
        {
            return $"For {playerName}, start with the highest-ranked same-position options on the Transfers page. A future AI version will discuss those live recommendations directly in this chat.";
        }

        return "I can discuss injuries, captaincy, lineup choices, and transfers. This first version uses a mocked response while the AI reasoning layer is being prepared.";
    }

    private static CoachRecommendationType GetRecommendationType(string message)
    {
        if (message.Contains("injur", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("doubt", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("available", StringComparison.OrdinalIgnoreCase))
        {
            return CoachRecommendationType.Availability;
        }

        if (message.Contains("sell", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transfer out", StringComparison.OrdinalIgnoreCase))
        {
            return CoachRecommendationType.Transfer;
        }

        if (message.Contains("replace", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("who", StringComparison.OrdinalIgnoreCase))
        {
            return CoachRecommendationType.Replacement;
        }

        return CoachRecommendationType.General;
    }

    private static decimal GetConfidence(CoachRecommendationType recommendationType, Player? player) =>
        recommendationType switch
        {
            CoachRecommendationType.Availability when player is not null => 78m,
            CoachRecommendationType.Transfer when player is not null => 68m,
            CoachRecommendationType.Replacement when player is not null => 64m,
            CoachRecommendationType.General => 35m,
            _ => 45m
        };

    private static bool IsPlayerMentioned(string message, Player player) =>
        ContainsName(message, player.DisplayName) ||
        ContainsName(message, player.FirstName) ||
        ContainsName(message, player.LastName);

    private static bool ContainsName(string message, string name) =>
        !string.IsNullOrWhiteSpace(name) && message.Contains(name, StringComparison.OrdinalIgnoreCase);

    private static CoachPlayerInfo MapPlayer(Player player, BootstrapData bootstrap)
    {
        var teamName = bootstrap.Teams.FirstOrDefault(team => team.Id == player.TeamId)?.Name ?? "Unknown team";
        var position = bootstrap.PlayerPositions.FirstOrDefault(item => item.Id == player.PositionId)?.ShortName ?? "Unknown";
        return new(
            player.Id,
            player.DisplayName,
            teamName,
            position,
            player.Status,
            player.ChanceOfPlayingNextRound,
            PlayerPhotoUrl.FromCode(player.Code));
    }
}