using Backend.Models;
using Backend.Recommendation.Transfer.Models;
using Backend.Services;

namespace Backend.Recommendation.Transfer;

public sealed class TransferRecommendationService(
    IFplDataService fplDataService,
    IProjectedPointsCalculator projectionCalculator,
    ITransferRecommendationEngine recommendationEngine,
    TimeProvider timeProvider) : ITransferRecommendationService
{
    private const int ProjectionConcurrency = 8;

    public async Task<TransferRecommendationResponse> GetRecommendationsAsync(
        int teamId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (teamId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(teamId), "The team ID must be positive.");
        }

        if (limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The result limit must be between 1 and 50.");
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
        var squadPlayers = squad.Picks.Select(pick => players.GetValueOrDefault(pick.PlayerId)
            ?? throw new KeyNotFoundException($"FPL player {pick.PlayerId} was not found.")).ToArray();
        var ownedIds = squadPlayers.Select(player => player.Id).ToHashSet();
        var saleValues = squad.Picks
            .Select(pick => pick.SellingPrice ?? players[pick.PlayerId].Price)
            .OrderByDescending(price => price)
            .Take(2)
            .ToArray();
        var availableMarket = bootstrap.Players
            .Where(player => !ownedIds.Contains(player.Id))
            .Where(player => player.Status is "a" or "d")
            .ToArray();
        var minimumIncomingPrice = availableMarket.Min(player => player.Price);
        var maximumSinglePurchase = manager.Bank + saleValues.Sum() - minimumIncomingPrice;
        var eligibleMarket = availableMarket
            .Where(player => player.Price <= maximumSinglePurchase)
            .Where(player => squadPlayers.Any(playerOut => playerOut.PositionId == player.PositionId))
            .ToArray();
        var contexts = await ProjectPlayersAsync(
            squadPlayers.Concat(eligibleMarket).DistinctBy(player => player.Id),
            teams,
            positions,
            cancellationToken);
        var contextsById = contexts.ToDictionary(context => context.Player.Id);
        var squadContexts = squad.Picks.Select(pick => contextsById[pick.PlayerId] with { SellingPrice = pick.SellingPrice }).ToArray();
        var marketContexts = eligibleMarket.Select(player => contextsById[player.Id]).ToArray();
        var recommendations = recommendationEngine.Rank(squadContexts, marketContexts, manager.Bank, limit);
        var combinations = recommendationEngine.RankCombinations(squadContexts, marketContexts, manager.Bank, limit);

        return new(
            teamId,
            manager.CurrentGameweek,
            timeProvider.GetUtcNow(),
            manager.Bank / 10m,
            recommendations,
            combinations);
    }

    public async Task<TransferRecommendationResponse> GetReplacementRecommendationsAsync(
        int teamId,
        int playerOutId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (teamId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(teamId), "The team ID must be positive.");
        }

        if (playerOutId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerOutId), "The outgoing player ID must be positive.");
        }

        if (limit is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The replacement limit must be between 1 and 5.");
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
        var squadPlayers = squad.Picks.Select(pick => players.GetValueOrDefault(pick.PlayerId)
            ?? throw new KeyNotFoundException($"FPL player {pick.PlayerId} was not found.")).ToArray();
        if (squadPlayers.All(player => player.Id != playerOutId))
        {
            throw new KeyNotFoundException($"Player {playerOutId} was not found in the connected 15-player squad.");
        }

        var playerOut = squadPlayers.Single(player => player.Id == playerOutId);
        var ownedIds = squadPlayers.Select(player => player.Id).ToHashSet();
        var salePrice = squad.Picks.Single(pick => pick.PlayerId == playerOutId).SellingPrice ?? playerOut.Price;
        var market = bootstrap.Players
            .Where(player => !ownedIds.Contains(player.Id))
            .Where(player => player.PositionId == playerOut.PositionId)
            .Where(player => player.Status is "a" or "d")
            .Where(player => player.Price <= salePrice + manager.Bank)
            .ToArray();
        var contexts = await ProjectPlayersAsync(squadPlayers.Concat(market), teams, positions, cancellationToken);
        var contextsById = contexts.ToDictionary(context => context.Player.Id);
        var squadContexts = squad.Picks.Select(pick => contextsById[pick.PlayerId] with { SellingPrice = pick.SellingPrice }).ToArray();
        var marketContexts = market.Select(player => contextsById[player.Id]).ToArray();
        var recommendations = recommendationEngine.RankReplacements(squadContexts, marketContexts, manager.Bank, playerOutId, limit);

        return new(
            teamId,
            manager.CurrentGameweek,
            timeProvider.GetUtcNow(),
            manager.Bank / 10m,
            recommendations,
            [],
            salePrice / 10m);
    }

    private async Task<IReadOnlyList<TransferPlayerContext>> ProjectPlayersAsync(
        IEnumerable<Player> players,
        IReadOnlyDictionary<int, Team> teams,
        IReadOnlyDictionary<int, PlayerPosition> positions,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(ProjectionConcurrency);
        var tasks = players.Select(async player =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var history = await fplDataService.GetPlayerHistoryAsync(player.Id, cancellationToken);
                var nextFixtures = history.Fixtures
                    .Where(fixture => fixture.Gameweek is not null)
                    .OrderBy(fixture => fixture.Gameweek)
                    .ThenBy(fixture => fixture.Kickoff)
                    .Take(3)
                    .Select(fixture =>
                    {
                        var opponentTeamId = fixture.IsHome ? fixture.AwayTeamId : fixture.HomeTeamId;
                        var opponent = teams.GetValueOrDefault(opponentTeamId)?.ShortName ?? "TBC";
                        return $"{opponent} ({(fixture.IsHome ? "H" : "A")})";
                    })
                    .ToArray();
                return new TransferPlayerContext(
                    player,
                    teams.GetValueOrDefault(player.TeamId)?.Name ?? "Unknown team",
                    positions.GetValueOrDefault(player.PositionId)?.ShortName ?? "Unknown",
                    projectionCalculator.Calculate(player, history),
                    NextFixtures: nextFixtures);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }
}