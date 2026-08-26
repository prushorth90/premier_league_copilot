using Backend.Recommendation.Captain.Models;
using Backend.Services;

namespace Backend.Recommendation.Captain;

public sealed class CaptainRecommendationService(
    IFplDataService fplDataService,
    IProjectedPointsCalculator projectionCalculator,
    ICaptainRankingCalculator rankingCalculator,
    TimeProvider timeProvider) : ICaptainRecommendationService
{
    public async Task<CaptainRecommendation> GetRecommendationAsync(
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
        var starters = squad.Picks.Where(pick => pick.Position <= 11).ToArray();
        if (starters.Length < 2)
        {
            throw new InvalidOperationException("At least two starting players are required for captain recommendations.");
        }

        var players = bootstrap.Players.ToDictionary(player => player.Id);
        var teams = bootstrap.Teams.ToDictionary(team => team.Id);
        var positions = bootstrap.PlayerPositions.ToDictionary(position => position.Id);
        var contexts = await Task.WhenAll(starters.Select(async pick =>
        {
            var player = players.GetValueOrDefault(pick.PlayerId)
                ?? throw new KeyNotFoundException($"FPL player {pick.PlayerId} was not found.");
            var history = await fplDataService.GetPlayerHistoryAsync(player.Id, cancellationToken);
            return new CaptainCandidateContext(
                player,
                teams.GetValueOrDefault(player.TeamId)?.Name ?? "Unknown team",
                positions.GetValueOrDefault(player.PositionId)?.ShortName ?? "Unknown",
                projectionCalculator.Calculate(player, history));
        }));
        var ranked = rankingCalculator.Rank(contexts);

        return new(
            teamId,
            manager.CurrentGameweek,
            timeProvider.GetUtcNow(),
            ranked[0],
            ranked[1],
            ranked.Skip(2).Take(3).ToArray());
    }
}