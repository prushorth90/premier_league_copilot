using Backend.Coach.Models;
using Backend.Models;
using Backend.Services;

namespace Backend.Coach;

public sealed class FplCoachService(
    IFplDataService fplDataService,
    IFplCoachOrchestrator orchestrator,
    TimeProvider timeProvider,
    ILogger<FplCoachService> logger) : ICoachService
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
        var playerInfo = matchedPlayer is null
            ? null
            : MapPlayer(matchedPlayer, bootstrap);
        var context = CreateContext(
            teamId,
            manager,
            squad,
            bootstrap);
        logger.LogInformation(
            "Starting AI Coach orchestration for TeamId {TeamId} and PlayerId {PlayerId}",
            teamId,
            matchedPlayer?.Id);
        var orchestration = await orchestrator.OrchestrateAsync(
            context,
            matchedPlayer?.Id,
            normalizedMessage,
            progressSink,
            cancellationToken);
        await ReportAsync(progressSink, "preparing-answer", "Preparing recommendation", cancellationToken);
        var reply = ComposeReply(
            orchestration.RecommendationType,
            orchestration.Availability,
            orchestration.Fixtures,
            orchestration.Recommendation);
        var structuredRecommendation = orchestration.Recommendation is not null && playerInfo is not null
            ? MapStructuredRecommendation(playerInfo, orchestration.Recommendation)
            : null;

        return new CoachChatResponse(
            reply,
            teamId,
            timeProvider.GetUtcNow(),
            false,
                orchestration.RecommendationType,
                orchestration.Confidence,
            playerInfo,
                orchestration.Availability,
                orchestration.Fixtures,
                orchestration.Transfers,
                orchestration.Recommendation,
            structuredRecommendation);
    }

    private static string ComposeReply(
        CoachRecommendationType recommendationType,
        PlayerAvailabilityResult? availability,
        PlayerFixtureWindowResult? fixtures,
        PlayerRecommendationResult? recommendation)
    {
        if (recommendation is not null)
        {
            var deterministic = $"Deterministic recommendation: {recommendation.Action.ToString().ToUpperInvariant()}. "
                + $"{recommendation.Reason} Confidence: {recommendation.Confidence:0}%. "
                + $"Projected impact: {recommendation.ProjectedImpact:+0.00;-0.00;0.00} points over {recommendation.ProjectionGameweeks} gameweeks.";
            return recommendationType == CoachRecommendationType.Availability
                && availability is not null
                && availability.Status != "i"
                    ? $"Official FPL data does not confirm that {availability.Player.PlayerName} is injured. Current status: {availability.StatusDescription}. {deterministic}"
                    : deterministic;
        }

        if (recommendationType == CoachRecommendationType.Availability && availability is not null)
        {
            var chance = availability.ChanceOfPlayingNextRound is int value ? $" Chance of playing: {value}%." : string.Empty;
            var expectedReturn = availability.ExpectedReturn is not null ? $" Expected return: {availability.ExpectedReturn}." : string.Empty;
            return availability.Status == "i"
                ? $"Official FPL data confirms that {availability.Player.PlayerName} is injured.{chance}{expectedReturn} Confidence: {availability.Confidence:0}%."
                : $"Official FPL data does not confirm that {availability.Player.PlayerName} is injured. Current status: {availability.StatusDescription}.{chance}{expectedReturn} Confidence: {availability.Confidence:0}%.";
        }

        if (fixtures is not null)
        {
            return fixtures.Explanation;
        }

        return "I could not identify a supported injury, fixture, or transfer question for a player in the connected squad.";
    }

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