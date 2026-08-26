using Backend.Recommendation.Lineup.Models;
using Backend.Services;

namespace Backend.Recommendation.Lineup;

public sealed class LineupRecommendationService(
    IFplDataService fplDataService,
    IProjectedPointsCalculator projectionCalculator,
    ILineupOptimizer lineupOptimizer,
    TimeProvider timeProvider) : ILineupRecommendationService
{
    public async Task<LineupRecommendation> GetRecommendationAsync(
        int teamId,
        CancellationToken cancellationToken)
    {
        if (teamId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(teamId), "The team ID must be positive.");
        }

        var managerTask = fplDataService.GetManagerAsync(teamId, cancellationToken);
        var bootstrapTask = fplDataService.GetBootstrapDataAsync(cancellationToken);
        await Task.WhenAll(managerTask, bootstrapTask);
        var manager = await managerTask;
        var bootstrap = await bootstrapTask;
        var squad = await fplDataService.GetManagerPicksAsync(teamId, manager.CurrentGameweek, cancellationToken);
        var players = bootstrap.Players.ToDictionary(player => player.Id);
        var teams = bootstrap.Teams.ToDictionary(team => team.Id);
        var positions = bootstrap.PlayerPositions.ToDictionary(position => position.Id);
        var contexts = await Task.WhenAll(squad.Picks.Select(async pick =>
        {
            var player = players.GetValueOrDefault(pick.PlayerId)
                ?? throw new KeyNotFoundException($"FPL player {pick.PlayerId} was not found.");
            var history = await fplDataService.GetPlayerHistoryAsync(player.Id, cancellationToken);
            return new LineupCandidateContext(
                player,
                teams.GetValueOrDefault(player.TeamId)?.Name ?? "Unknown team",
                positions.GetValueOrDefault(player.PositionId)?.ShortName ?? "Unknown",
                pick.Position,
                projectionCalculator.Calculate(player, history));
        }));
        var optimized = lineupOptimizer.Optimize(contexts);

        return new(
            teamId,
            manager.CurrentGameweek,
            timeProvider.GetUtcNow(),
            optimized.Formation,
            optimized.StartingXi,
            optimized.Bench,
            optimized.Changes);
    }
}