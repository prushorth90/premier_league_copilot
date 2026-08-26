using Backend.Coach.Models;
using Backend.Models;
using Backend.Services;

namespace Backend.Coach;

public sealed class CopilotCoachService(
    IFplDataService fplDataService,
    ICopilotChatClient copilotChatClient,
    IFplCoachFactService factService,
    IPlayerRecommendationService recommendationService,
    TimeProvider timeProvider) : ICoachService
{
    public Task<CoachChatResponse> ReplyAsync(
        int teamId,
        string message,
        CancellationToken cancellationToken) =>
        ReplyCoreAsync(teamId, message, null, cancellationToken);

    public Task<CoachChatResponse> ReplyWithProgressAsync(
        int teamId,
        string message,
        ICoachProgressSink progressSink,
        CancellationToken cancellationToken) =>
        ReplyCoreAsync(teamId, message, progressSink, cancellationToken);

    private async Task<CoachChatResponse> ReplyCoreAsync(
        int teamId,
        string message,
        ICoachProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        var normalizedMessage = message.Trim();
        await ReportAsync(progressSink, "loading-squad", "Loading squad context", cancellationToken);
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
        PlayerAvailabilityResult? availability = null;
        PlayerRecommendationResult? recommendation = null;
        if (matchedPlayer is not null && recommendationType == CoachRecommendationType.Availability)
        {
            await ReportAsync(progressSink, "checking-availability", "Checking player availability", cancellationToken);
            availability = factService.GetPlayerAvailability(context, matchedPlayer.Id);
            if (MayMissMatches(availability))
            {
                await ReportAsync(progressSink, "analyzing-fixtures", "Analyzing upcoming fixtures", cancellationToken);
                await ReportAsync(progressSink, "comparing-replacements", "Comparing replacements", cancellationToken);
            }
            recommendation = await recommendationService.GetRecommendationIfAtRiskAsync(
                context,
                availability,
                GetDecisionGameweeks(normalizedMessage),
                3,
                cancellationToken);
        }
        else if (matchedPlayer is not null && RequiresRecommendation(recommendationType))
        {
            await ReportAsync(progressSink, "checking-availability", "Checking player availability", cancellationToken);
            await ReportAsync(progressSink, "analyzing-fixtures", "Analyzing upcoming fixtures", cancellationToken);
            await ReportAsync(progressSink, "comparing-replacements", "Comparing replacements", cancellationToken);
            recommendation = await recommendationService.GetRecommendationAsync(
                context,
                matchedPlayer.Id,
                GetDecisionGameweeks(normalizedMessage),
                3,
                cancellationToken);
            availability = recommendation.Availability;
        }

        PlayerFixtureWindowResult? fixtures = recommendation?.Fixtures;
        if (fixtures is null && recommendationType == CoachRecommendationType.Fixture && matchedPlayer is not null)
        {
            await ReportAsync(progressSink, "analyzing-fixtures", "Analyzing upcoming fixtures", cancellationToken);
            fixtures = await factService.GetUpcomingFixturesAsync(
                context,
                matchedPlayer.Id,
                GetRequestedGameweeks(normalizedMessage),
                cancellationToken);
        }
        var transfers = recommendation?.Transfers;
        var confidence = recommendation?.Confidence
            ?? availability?.Confidence
            ?? transfers?.Candidates.FirstOrDefault()?.Confidence
            ?? GetConfidence(recommendationType, matchedPlayer);
        var grounding = CreateGrounding(availability, fixtures, transfers, recommendation);
        await ReportAsync(progressSink, "preparing-answer", "Preparing recommendation", cancellationToken);
        var reply = await copilotChatClient.GenerateAsync(normalizedMessage, context, grounding, cancellationToken);
        reply = EnsureRecommendationIsGrounded(reply, recommendation);
        reply = EnsureAvailabilityClaimIsGrounded(
            reply,
            recommendationType == CoachRecommendationType.Availability ? availability : null);
        var structuredRecommendation = recommendation is not null && playerInfo is not null
            ? MapStructuredRecommendation(playerInfo, recommendation)
            : null;

        return new CoachChatResponse(
            reply,
            teamId,
            timeProvider.GetUtcNow(),
            false,
            recommendationType,
            confidence,
            playerInfo,
            availability,
            fixtures,
            transfers,
            recommendation,
            structuredRecommendation);
    }

    private static bool MayMissMatches(PlayerAvailabilityResult availability) =>
        availability.Status is "d" or "i" or "s" or "u" or "n"
        || availability.ChanceOfPlayingNextRound is int chance && chance < 75;

    private static ValueTask ReportAsync(
        ICoachProgressSink? progressSink,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        progressSink?.ReportAsync(new CoachProgressUpdate(code, message), cancellationToken) ?? ValueTask.CompletedTask;

    private static CoachStructuredRecommendation MapStructuredRecommendation(
        CoachPlayerInfo player,
        PlayerRecommendationResult recommendation)
    {
        var availability = recommendation.Availability;
        var fixtures = recommendation.Fixtures;
        var replacement = recommendation.RecommendedReplacement;
        return new CoachStructuredRecommendation(
            player,
            recommendation.Action,
            recommendation.Confidence,
            new CoachInjuryStatus(
                availability.Status,
                availability.StatusDescription,
                availability.IsAvailable,
                availability.ChanceOfPlayingNextRound,
                availability.ExpectedReturn),
            new CoachFixtureSummary(
                fixtures.RequestedGameweeks,
                fixtures.ScheduleRating,
                fixtures.AverageDifficulty,
                fixtures.AggregateScore,
                fixtures.Fixtures),
            replacement is null
                ? null
                : new CoachSuggestedReplacement(
                    replacement.Player.PlayerId,
                    replacement.Player.PlayerName,
                    replacement.Player.TeamName,
                    replacement.Player.Position,
                    replacement.Player.Price,
                    replacement.PriceDifference,
                    replacement.ProjectedPointDifference,
                    replacement.Reason),
            recommendation.ProjectedImpact,
            recommendation.ProjectionGameweeks,
            recommendation.Reason);
    }

    private static CoachSpecialistGrounding CreateGrounding(
        PlayerAvailabilityResult? availability,
        PlayerFixtureWindowResult? fixtures,
        PlayerReplacementResult? transfers,
        PlayerRecommendationResult? recommendation)
    {
        var invokedAgents = new List<string>(3);
        if (availability is not null)
        {
            invokedAgents.Add(FplCoachAgents.InjurySpecialistName);
        }

        if (fixtures is not null)
        {
            invokedAgents.Add(FplCoachAgents.FixtureSpecialistName);
        }

        if (transfers is not null)
        {
            invokedAgents.Add(FplCoachAgents.TransferSpecialistName);
        }

        return new CoachSpecialistGrounding(invokedAgents, availability, fixtures, transfers, recommendation);
    }

    private static string EnsureRecommendationIsGrounded(
        string reply,
        PlayerRecommendationResult? recommendation) =>
        recommendation is null
            ? reply
            : $"Deterministic recommendation: {recommendation.Action.ToString().ToUpperInvariant()}. {recommendation.Reason} {reply}";

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

        if (message.Contains("bench", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("hold", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("start", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("what should", StringComparison.OrdinalIgnoreCase))
        {
            return CoachRecommendationType.Recommendation;
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
            CoachRecommendationType.Recommendation when player is not null => 80m,
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

    private static int GetDecisionGameweeks(string message)
    {
        var requested = GetRequestedGameweeks(message);
        return requested == 3 && !message.Contains("3", StringComparison.OrdinalIgnoreCase) ? 5 : requested;
    }

    private static bool RequiresRecommendation(CoachRecommendationType recommendationType) =>
        recommendationType is CoachRecommendationType.Recommendation
            or CoachRecommendationType.Transfer
            or CoachRecommendationType.Replacement;

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