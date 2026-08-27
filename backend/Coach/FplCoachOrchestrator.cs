using Backend.Coach.Models;

namespace Backend.Coach;

public sealed class FplCoachOrchestrator(
    IFplCoachFactService factService,
    IPlayerRecommendationService recommendationService,
    IFplCoachAgentProvider agentProvider,
    ILogger<FplCoachOrchestrator> logger) : IFplCoachOrchestrator
{
    public async Task<FplCoachOrchestrationResult> OrchestrateAsync(
        FplCoachContext context,
        int? playerId,
        string message,
        ICoachProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        var agents = agentProvider.GetAgents();
        var parent = agents.Single(agent => agent.Name == FplCoachAgents.ParentName);
        var recommendationType = GetRecommendationType(message);
        PlayerAvailabilityResult? availability = null;
        PlayerFixtureWindowResult? fixtures = null;
        PlayerReplacementResult? transfers = null;
        PlayerRecommendationResult? recommendation = null;

        if (playerId is int ownedPlayerId && recommendationType == CoachRecommendationType.Availability)
        {
            var injuryAgent = RequireSpecialist(agents, FplCoachAgents.InjurySpecialistName, FplCoachAgents.AvailabilityTool);
            await ReportAsync(progressSink, "checking-availability", "Checking player availability", cancellationToken);
            availability = factService.GetPlayerAvailability(context, ownedPlayerId);
            if (MayMissMatches(availability))
            {
                RequireSpecialist(agents, FplCoachAgents.FixtureSpecialistName, FplCoachAgents.FixturesTool);
                RequireSpecialist(agents, FplCoachAgents.TransferSpecialistName, FplCoachAgents.TransfersTool);
                await ReportAsync(progressSink, "analyzing-fixtures", "Analyzing upcoming fixtures", cancellationToken);
                await ReportAsync(progressSink, "comparing-replacements", "Comparing replacements", cancellationToken);
            }

            recommendation = await recommendationService.GetRecommendationIfAtRiskAsync(
                context,
                availability,
                GetDecisionGameweeks(message),
                3,
                cancellationToken);
            logger.LogDebug("Applied specialist definition {AgentName}", injuryAgent.Name);
        }
        else if (playerId is int recommendationPlayerId && RequiresRecommendation(recommendationType))
        {
            RequireSpecialist(agents, FplCoachAgents.InjurySpecialistName, FplCoachAgents.AvailabilityTool);
            RequireSpecialist(agents, FplCoachAgents.FixtureSpecialistName, FplCoachAgents.FixturesTool);
            RequireSpecialist(agents, FplCoachAgents.TransferSpecialistName, FplCoachAgents.TransfersTool);
            await ReportAsync(progressSink, "checking-availability", "Checking player availability", cancellationToken);
            await ReportAsync(progressSink, "analyzing-fixtures", "Analyzing upcoming fixtures", cancellationToken);
            await ReportAsync(progressSink, "comparing-replacements", "Comparing replacements", cancellationToken);
            recommendation = await recommendationService.GetRecommendationAsync(
                context,
                recommendationPlayerId,
                GetDecisionGameweeks(message),
                3,
                cancellationToken);
            availability = recommendation.Availability;
        }
        else if (playerId is int fixturePlayerId && recommendationType == CoachRecommendationType.Fixture)
        {
            RequireSpecialist(agents, FplCoachAgents.FixtureSpecialistName, FplCoachAgents.FixturesTool);
            await ReportAsync(progressSink, "analyzing-fixtures", "Analyzing upcoming fixtures", cancellationToken);
            fixtures = await factService.GetUpcomingFixturesAsync(
                context,
                fixturePlayerId,
                GetRequestedGameweeks(message),
                cancellationToken);
        }

        fixtures ??= recommendation?.Fixtures;
        transfers = recommendation?.Transfers;
        var grounding = CreateGrounding(availability, fixtures, transfers, recommendation);
        var confidence = recommendation?.Confidence
            ?? availability?.Confidence
            ?? transfers?.Candidates.FirstOrDefault()?.Confidence
            ?? GetDefaultConfidence(recommendationType, playerId is not null);
        logger.LogInformation(
            "AI Coach parent {ParentAgent} used specialist definitions {InvokedAgents}; deterministic action {RecommendationAction}",
            parent.Name,
            grounding.InvokedAgents.Count == 0 ? "none" : string.Join(",", grounding.InvokedAgents),
            recommendation?.Action.ToString() ?? "none");

        return new(
            recommendationType,
            confidence,
            availability,
            fixtures,
            transfers,
            recommendation,
            grounding);
    }

    private static FplCoachAgentDefinition RequireSpecialist(
        IReadOnlyList<FplCoachAgentDefinition> agents,
        string name,
        string requiredTool)
    {
        var agent = agents.Single(item => item.Name == name);
        if (!agent.Tools.SequenceEqual([requiredTool], StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Agent '{name}' does not have the required restricted tool '{requiredTool}'.");
        }

        return agent;
    }

    private static CoachSpecialistGrounding CreateGrounding(
        PlayerAvailabilityResult? availability,
        PlayerFixtureWindowResult? fixtures,
        PlayerReplacementResult? transfers,
        PlayerRecommendationResult? recommendation)
    {
        var invokedAgents = new List<string>(3);
        if (availability is not null) invokedAgents.Add(FplCoachAgents.InjurySpecialistName);
        if (fixtures is not null) invokedAgents.Add(FplCoachAgents.FixtureSpecialistName);
        if (transfers is not null) invokedAgents.Add(FplCoachAgents.TransferSpecialistName);
        return new(invokedAgents, availability, fixtures, transfers, recommendation);
    }

    private static bool MayMissMatches(PlayerAvailabilityResult availability) =>
        availability.Status is "d" or "i" or "s" or "u" or "n"
        || availability.ChanceOfPlayingNextRound is int chance && chance < 75;

    private static CoachRecommendationType GetRecommendationType(string message)
    {
        if (ContainsAny(message, "injur", "doubt", "available")) return CoachRecommendationType.Availability;
        if (ContainsAny(message, "fixture", "schedule", "opponent")) return CoachRecommendationType.Fixture;
        if (ContainsAny(message, "bench", "hold", "start", "what should")) return CoachRecommendationType.Recommendation;
        if (ContainsAny(message, "sell", "transfer out")) return CoachRecommendationType.Transfer;
        if (ContainsAny(message, "replace", "who")) return CoachRecommendationType.Replacement;
        return CoachRecommendationType.General;
    }

    private static bool ContainsAny(string message, params string[] values) =>
        values.Any(value => message.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static int GetRequestedGameweeks(string message)
    {
        for (var gameweeks = 1; gameweeks <= 5; gameweeks++)
        {
            if (ContainsAny(message, $"{gameweeks} fixture", $"{gameweeks} gameweek", $"next {gameweeks}"))
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

    private static bool RequiresRecommendation(CoachRecommendationType type) =>
        type is CoachRecommendationType.Recommendation or CoachRecommendationType.Transfer or CoachRecommendationType.Replacement;

    private static decimal GetDefaultConfidence(CoachRecommendationType type, bool hasPlayer) => type switch
    {
        CoachRecommendationType.Availability when hasPlayer => 78m,
        CoachRecommendationType.Fixture when hasPlayer => 90m,
        CoachRecommendationType.Recommendation when hasPlayer => 80m,
        CoachRecommendationType.Transfer when hasPlayer => 68m,
        CoachRecommendationType.Replacement when hasPlayer => 64m,
        CoachRecommendationType.General => 35m,
        _ => 45m
    };

    private static ValueTask ReportAsync(
        ICoachProgressSink? sink,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        sink?.ReportAsync(new CoachProgressUpdate(code, message), cancellationToken) ?? ValueTask.CompletedTask;
}