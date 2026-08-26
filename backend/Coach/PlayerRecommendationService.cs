using Backend.Coach.Models;

namespace Backend.Coach;

public sealed class PlayerRecommendationService(IFplCoachFactService factService) : IPlayerRecommendationService
{
    public async Task<PlayerRecommendationResult> GetRecommendationAsync(
        FplCoachContext context,
        int playerId,
        int gameweeks,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        var availability = factService.GetPlayerAvailability(context, playerId);
        return await AnalyzeAsync(context, availability, gameweeks, candidateLimit, cancellationToken);
    }

    public async Task<PlayerRecommendationResult?> GetRecommendationIfAtRiskAsync(
        FplCoachContext context,
        PlayerAvailabilityResult verifiedAvailability,
        int gameweeks,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        if (!MayMissMatches(verifiedAvailability))
        {
            return null;
        }

        return await AnalyzeAsync(context, verifiedAvailability, gameweeks, candidateLimit, cancellationToken);
    }

    private async Task<PlayerRecommendationResult> AnalyzeAsync(
        FplCoachContext context,
        PlayerAvailabilityResult availability,
        int gameweeks,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        var playerId = availability.Player.PlayerId;
        var fixturesTask = factService.GetUpcomingFixturesAsync(context, playerId, gameweeks, cancellationToken);
        var transfersTask = factService.GetTransferCandidatesAsync(context, playerId, candidateLimit, cancellationToken);
        await Task.WhenAll(fixturesTask, transfersTask);

        return PlayerRecommendationPolicy.Evaluate(
            availability,
            await fixturesTask,
            await transfersTask);
    }

    private static bool MayMissMatches(PlayerAvailabilityResult availability) =>
        availability.Status is "d" or "i" or "s" or "u" or "n"
        || availability.ChanceOfPlayingNextRound is int chance && chance < 75;
}