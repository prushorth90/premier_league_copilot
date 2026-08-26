using Backend.Models;
using Backend.Recommendation;
using Backend.Recommendation.Captain;
using Backend.Recommendation.Captain.Factors;
using Backend.Recommendation.Captain.Models;
using Backend.Recommendation.Models;
using Backend.Services;

namespace Backend.Tests;

public class CaptainRecommendationServiceTests
{
    [Fact]
    public async Task GetRecommendationAsyncRanksOnlyStartingPlayers()
    {
        var dataService = new StubFplDataService();
        var service = new CaptainRecommendationService(
            dataService,
            new StubProjectionCalculator(),
            new CaptainRankingCalculator(
            [
                new ProjectedPointsCaptainFactor(),
                new ExpectedMinutesCaptainFactor(),
                new FixtureQualityCaptainFactor(),
                new AttackingPotentialCaptainFactor(),
                new AvailabilityCaptainFactor()
            ]),
            TimeProvider.System);

        var result = await service.GetRecommendationAsync(42, CancellationToken.None);

        Assert.Equal(42, result.TeamId);
        Assert.Equal(3, result.Gameweek);
        Assert.Equal(2, result.BestCaptain.PlayerId);
        Assert.Equal(1, result.ViceCaptain.PlayerId);
        Assert.Empty(result.Alternatives);
        Assert.Equal([1, 2], dataService.RequestedHistoryIds.Order());
        Assert.DoesNotContain(3, dataService.RequestedHistoryIds);
    }

    private sealed class StubProjectionCalculator : IProjectedPointsCalculator
    {
        public PlayerProjection Calculate(Player player, PlayerHistory history) => new(
            player.Id,
            player.DisplayName,
            DateTimeOffset.UtcNow,
            90m,
            [new ProjectionHorizon(
                1,
                player.Id == 2 ? 8m : 6m,
                [
                    new ProjectionFactorBreakdown("Fixture difficulty", 0m, "Fixture"),
                    new ProjectionFactorBreakdown("Venue", 0m, "Venue")
                ],
                [])]);
    }

    private sealed class StubFplDataService : IFplDataService
    {
        public List<int> RequestedHistoryIds { get; } = [];

        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) => Task.FromResult(new BootstrapData(
            [],
            [new Team(1, 1, "Test FC", "TST", 3, 3, 3)],
            [new PlayerPosition(3, "Midfielder", "MID", 5, 2, 5)],
            [CreatePlayer(1), CreatePlayer(2), CreatePlayer(3)]));

        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => Task.FromResult(new Manager(
            managerId, "Ada", "Manager", "Expected Goals", 1, 3, 100, 1, 50, 1, 0, 1000));

        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => Task.FromResult(new Squad(
            null,
            new SquadGameweekSummary(gameweek, 50, 100, 1, 0, 1000, 0, 0, 0),
            [
                new SquadPick(1, 1, 1, false, false, 3),
                new SquadPick(2, 2, 1, false, false, 3),
                new SquadPick(3, 12, 0, false, false, 3)
            ]));

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken)
        {
            RequestedHistoryIds.Add(playerId);
            return Task.FromResult(new PlayerHistory([], [], []));
        }

        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Fixture>>([]);

        private static Player CreatePlayer(int id) => new(
            id, id, $"Player {id}", "", $"Player {id}", 1, 3, 50, 0, 0, 0, 0,
            id * 0.1m, id * 0.05m, "a", "", null);
    }
}