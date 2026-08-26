using Backend.Models;
using Backend.Recommendation;
using Backend.Recommendation.Models;
using Backend.Recommendation.Transfer;
using Backend.Services;

namespace Backend.Tests;

public class TransferRecommendationServiceTests
{
    [Fact]
    public async Task GetRecommendationsAsyncProjectsOnlyOwnedAndEligibleMarketPlayers()
    {
        var dataService = new StubFplDataService();
        var service = new TransferRecommendationService(
            dataService,
            new StubProjectionCalculator(),
            new TransferRecommendationEngine(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.GetRecommendationsAsync(42, 2, CancellationToken.None);

        Assert.Equal(42, result.TeamId);
        Assert.Equal(3, result.Gameweek);
        Assert.Equal(0.5m, result.Bank);
        Assert.Equal(2, result.Recommendations.Count);
        Assert.All(result.Recommendations, recommendation => Assert.Equal(101, recommendation.PlayerIn.PlayerId));
        Assert.Equal(Enumerable.Range(1, 15).Append(101).Order(), dataService.RequestedHistoryIds.Order());
        Assert.DoesNotContain(102, dataService.RequestedHistoryIds);
        Assert.DoesNotContain(103, dataService.RequestedHistoryIds);
        Assert.DoesNotContain(104, dataService.RequestedHistoryIds);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(42, 0)]
    [InlineData(42, 51)]
    public async Task GetRecommendationsAsyncRejectsInvalidArguments(int teamId, int limit)
    {
        var service = new TransferRecommendationService(
            new StubFplDataService(),
            new StubProjectionCalculator(),
            new TransferRecommendationEngine(),
            TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetRecommendationsAsync(teamId, limit, CancellationToken.None));
    }

    private sealed class StubProjectionCalculator : IProjectedPointsCalculator
    {
        public PlayerProjection Calculate(Player player, PlayerHistory history)
        {
            var pointsPerGameweek = player.Id == 101 ? 10m : 4m;
            return new(
                player.Id,
                player.DisplayName,
                DateTimeOffset.UtcNow,
                90m,
                [
                    CreateHorizon(1, pointsPerGameweek),
                    CreateHorizon(3, pointsPerGameweek * 3m),
                    CreateHorizon(5, pointsPerGameweek * 5m)
                ]);
        }

        private static ProjectionHorizon CreateHorizon(int gameweeks, decimal points) => new(
            gameweeks,
            points,
            [new ProjectionFactorBreakdown("Fixture difficulty", 0m, "Fixture")],
            [new FixtureProjection(gameweeks, gameweeks, points, [])]);
    }

    private sealed class StubFplDataService : IFplDataService
    {
        public List<int> RequestedHistoryIds { get; } = [];

        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) => Task.FromResult(new BootstrapData(
            [],
            Enumerable.Range(1, 7).Select(id => new Team(id, id, $"Team {id}", $"T{id}", 3, 3, 3)).ToArray(),
            [
                new PlayerPosition(1, "Goalkeeper", "GKP", 2, 1, 1),
                new PlayerPosition(2, "Defender", "DEF", 5, 3, 5),
                new PlayerPosition(3, "Midfielder", "MID", 5, 2, 5),
                new PlayerPosition(4, "Forward", "FWD", 3, 1, 3)
            ],
            CreateSquadPlayers().Concat(
            [
                CreatePlayer(101, 6, 2, 55),
                CreatePlayer(102, 6, 2, 50, "i"),
                CreatePlayer(103, 6, 2, 100),
                CreatePlayer(104, 1, 4, 50)
            ]).ToArray()));

        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => Task.FromResult(new Manager(
            managerId, "Ada", "Manager", "Expected Goals", 1, 3, 100, 1, 50, 1, 5, 1000));

        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => Task.FromResult(new Squad(
            null,
            new SquadGameweekSummary(gameweek, 50, 100, 1, 5, 1000, 0, 0, 0),
            CreateSquadPlayers().Select((player, index) => new SquadPick(player.Id, index + 1, index < 11 ? 1 : 0, false, false, player.PositionId, 50, 50)).ToArray()));

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken)
        {
            RequestedHistoryIds.Add(playerId);
            return Task.FromResult(new PlayerHistory([], [], []));
        }

        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Fixture>>([]);

        private static IReadOnlyList<Player> CreateSquadPlayers() =>
        [
            CreatePlayer(1, 1, 1, 60), CreatePlayer(2, 2, 1, 60),
            CreatePlayer(3, 3, 2, 60), CreatePlayer(4, 4, 2, 60), CreatePlayer(5, 5, 2, 60), CreatePlayer(6, 1, 2, 60), CreatePlayer(7, 2, 2, 60),
            CreatePlayer(8, 3, 3, 60), CreatePlayer(9, 4, 3, 60), CreatePlayer(10, 5, 3, 60), CreatePlayer(11, 1, 3, 60), CreatePlayer(12, 2, 3, 60),
            CreatePlayer(13, 3, 4, 60), CreatePlayer(14, 4, 4, 60), CreatePlayer(15, 5, 4, 60)
        ];

        private static Player CreatePlayer(int id, int teamId, int positionId, int price, string status = "a") => new(
            id, id, $"Player {id}", "", $"Player {id}", teamId, positionId, price, 0, 0, 0, 0, 0, 0, status, "", null);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
