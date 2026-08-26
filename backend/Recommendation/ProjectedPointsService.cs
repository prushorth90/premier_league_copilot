using Backend.Services;

namespace Backend.Recommendation;

public sealed class ProjectedPointsService(
    IFplDataService fplDataService,
    IProjectedPointsCalculator calculator) : IProjectedPointsService
{
    public async Task<Models.PlayerProjection> GetPlayerProjectionAsync(
        int playerId,
        CancellationToken cancellationToken)
    {
        if (playerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId), "The player ID must be positive.");
        }

        var bootstrapTask = fplDataService.GetBootstrapDataAsync(cancellationToken);
        var historyTask = fplDataService.GetPlayerHistoryAsync(playerId, cancellationToken);
        await Task.WhenAll(bootstrapTask, historyTask);

        var player = (await bootstrapTask).Players.FirstOrDefault(item => item.Id == playerId)
            ?? throw new KeyNotFoundException($"FPL player {playerId} was not found.");

        return calculator.Calculate(player, await historyTask);
    }
}