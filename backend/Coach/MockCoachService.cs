using Backend.Coach.Models;
using Backend.Models;
using Backend.Services;

namespace Backend.Coach;

public sealed class CopilotCoachService(
    IFplDataService fplDataService,
    ICopilotChatClient copilotChatClient,
    IFplCoachFactService factService,
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
        if (squad.Picks.Count != 15 || squad.Picks.Select(pick => pick.PlayerId).Distinct().Count() != 15)
        {
            throw new InvalidOperationException("The connected FPL team did not return a valid 15-player squad.");
        }

        var squadPlayerIds = squad.Picks.Select(pick => pick.PlayerId).ToHashSet();
        var matchedPlayer = bootstrap.Players
            .Where(player => squadPlayerIds.Contains(player.Id))
            .OrderByDescending(player => player.DisplayName.Length)
            .FirstOrDefault(player => IsPlayerMentioned(normalizedMessage, player));
        var recommendationType = GetRecommendationType(normalizedMessage);
        var playerInfo = matchedPlayer is null
            ? null
            : MapPlayer(matchedPlayer, bootstrap);
        var context = CreateContext(
            teamId,
            manager,
            squad,
            bootstrap);
        var availability = recommendationType == CoachRecommendationType.Availability && matchedPlayer is not null
            ? factService.GetPlayerAvailability(context, matchedPlayer.Id)
            : null;
        var fixtures = recommendationType == CoachRecommendationType.Fixture && matchedPlayer is not null
            ? await factService.GetUpcomingFixturesAsync(
                context,
                matchedPlayer.Id,
                GetRequestedGameweeks(normalizedMessage),
                cancellationToken)
            : null;
        var confidence = availability?.Confidence ?? GetConfidence(recommendationType, matchedPlayer);
        var reply = await copilotChatClient.GenerateAsync(normalizedMessage, context, cancellationToken);
        reply = EnsureAvailabilityClaimIsGrounded(reply, availability);

        return new CoachChatResponse(
            reply,
            teamId,
            timeProvider.GetUtcNow(),
            false,
            recommendationType,
            confidence,
            playerInfo,
            availability,
            fixtures);
    }

    private static string EnsureAvailabilityClaimIsGrounded(
        string reply,
        PlayerAvailabilityResult? availability)
    {
        if (availability is null || availability.Status == "i")
        {
            return reply;
        }

        var requiredStatement = $"Official FPL data does not confirm that {availability.Player.PlayerName} is injured. Current status: {availability.StatusDescription}.";
        return reply.Contains("does not confirm", StringComparison.OrdinalIgnoreCase)
            ? reply
            : $"{requiredStatement} {reply}";
    }

    private static FplCoachContext CreateContext(
        int teamId,
        Manager manager,
        Squad squad,
        BootstrapData bootstrap)
    {
        var players = bootstrap.Players.ToDictionary(item => item.Id);
        var teams = bootstrap.Teams.ToDictionary(item => item.Id);
        var positions = bootstrap.PlayerPositions.ToDictionary(item => item.Id);
        return new FplCoachContext(
            teamId,
            manager.TeamName,
            manager.CurrentGameweek,
            manager.Bank / 10m,
            manager.TeamValue / 10m,
            squad.Picks.Select(pick =>
            {
                players.TryGetValue(pick.PlayerId, out var squadPlayer);
                return new FplCoachSquadPlayer(
                    pick.PlayerId,
                    squadPlayer?.DisplayName ?? "Unknown player",
                    squadPlayer is null ? "Unknown team" : teams.GetValueOrDefault(squadPlayer.TeamId)?.Name ?? "Unknown team",
                    squadPlayer is null ? "Unknown" : positions.GetValueOrDefault(squadPlayer.PositionId)?.ShortName ?? "Unknown",
                    (squadPlayer?.Price ?? 0) / 10m,
                    squadPlayer?.Status ?? "unknown",
                    squadPlayer?.News ?? string.Empty,
                    squadPlayer?.ChanceOfPlayingNextRound,
                    pick.Position <= 11,
                    pick.IsCaptain,
                    pick.IsViceCaptain);
            }).ToArray());
    }

    private static CoachRecommendationType GetRecommendationType(string message)
    {
        if (message.Contains("injur", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("doubt", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("available", StringComparison.OrdinalIgnoreCase))
        {
            return CoachRecommendationType.Availability;
        }

        if (message.Contains("fixture", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("schedule", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("opponent", StringComparison.OrdinalIgnoreCase))
        {
            return CoachRecommendationType.Fixture;
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
            CoachRecommendationType.Fixture when player is not null => 90m,
            CoachRecommendationType.Transfer when player is not null => 68m,
            CoachRecommendationType.Replacement when player is not null => 64m,
            CoachRecommendationType.General => 35m,
            _ => 45m
        };

    private static int GetRequestedGameweeks(string message)
    {
        for (var gameweeks = 1; gameweeks <= 5; gameweeks++)
        {
            if (message.Contains($"{gameweeks} fixture", StringComparison.OrdinalIgnoreCase) ||
                message.Contains($"{gameweeks} gameweek", StringComparison.OrdinalIgnoreCase) ||
                message.Contains($"next {gameweeks}", StringComparison.OrdinalIgnoreCase))
            {
                return gameweeks;
            }
        }

        return 3;
    }

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