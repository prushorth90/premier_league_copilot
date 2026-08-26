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
        var fixturesTask = factService.GetUpcomingFixturesAsync(context, playerId, gameweeks, cancellationToken);
        var transfersTask = factService.GetTransferCandidatesAsync(context, playerId, candidateLimit, cancellationToken);
        await Task.WhenAll(fixturesTask, transfersTask);

        return PlayerRecommendationPolicy.Evaluate(
            availability,
            await fixturesTask,
            await transfersTask);
    }
}