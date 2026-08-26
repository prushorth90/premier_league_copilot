using Backend.Configuration;
using Backend.ExternalClients;
using Backend.Services;
using Backend.Services.Caching;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

public class FplDataMappingTests
{
    [Fact]
    public async Task ServiceMapsEveryExternalFplResourceIntoDomainModels()
    {
        var service = new FplDataService(
            new StubFplApiClient(),
            new PassThroughCacheCoordinator(),
            new FplCachePolicyProvider(Options.Create(new FplApiOptions())));

        var bootstrap = await service.GetBootstrapDataAsync(CancellationToken.None);
        var fixtures = await service.GetFixturesAsync(CancellationToken.None);
        var manager = await service.GetManagerAsync(42, CancellationToken.None);
        var squad = await service.GetManagerPicksAsync(42, 8, CancellationToken.None);
        var history = await service.GetPlayerHistoryAsync(10, CancellationToken.None);

        var gameweek = Assert.Single(bootstrap.Gameweeks);
        Assert.Equal("Gameweek 8", gameweek.Name);
        Assert.True(gameweek.IsCurrent);
        var team = Assert.Single(bootstrap.Teams);
        Assert.Equal("Arsenal", team.Name);
        Assert.Equal(5, team.AwayStrength);
        var position = Assert.Single(bootstrap.PlayerPositions);
        Assert.Equal("MID", position.ShortName);
        Assert.Equal(5, position.SquadSize);
        var player = Assert.Single(bootstrap.Players);
        Assert.Equal("Test Player", player.DisplayName);
        Assert.Equal(7.5m, player.Form);
        Assert.Equal(18.2m, player.OwnershipPercentage);
        Assert.Equal(0.45m, player.ExpectedGoalsPer90);
        Assert.Equal(75, player.ChanceOfPlayingNextRound);

        var fixture = Assert.Single(fixtures);
        Assert.Equal(3, fixture.HomeScore);
        Assert.Equal(4, fixture.AwayDifficulty);
        Assert.Equal("Expected Goals", manager.TeamName);
        Assert.Equal(15, manager.Bank);

        var pick = Assert.Single(squad.Picks);
        Assert.Equal(10, pick.PlayerId);
        Assert.True(pick.IsCaptain);
        Assert.Equal(70, pick.PurchasePrice);
        Assert.Equal(74, pick.SellingPrice);
        Assert.Equal(8, squad.Summary.Gameweek);

        var playerFixture = Assert.Single(history.Fixtures);
        Assert.Equal("Gameweek 9", playerFixture.GameweekName);
        Assert.False(playerFixture.IsHome);
        var current = Assert.Single(history.CurrentSeason);
        Assert.Equal(90, current.Minutes);
        Assert.Equal(2, current.Assists);
        var past = Assert.Single(history.PreviousSeasons);
        Assert.Equal("2025/26", past.Season);
        Assert.Equal(180, past.Points);
    }

    private sealed class PassThroughCacheCoordinator : IFplCacheCoordinator
    {
        public Task<T> GetOrCreateAsync<T>(FplCachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken) => factory(cancellationToken);
    }

    private sealed class StubFplApiClient : IFplApiClient
    {
        private static readonly DateTimeOffset Kickoff = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

        public Task<FplBootstrapDto> GetBootstrapStaticAsync(CancellationToken cancellationToken) => Task.FromResult(new FplBootstrapDto(
            [new FplEventDto(8, "Gameweek 8", Kickoff.AddDays(-1), false, true, false, 55, 120)],
            [new FplTeamDto(1, 3, "Arsenal", "ARS", 4, 4, 5)],
            [new FplPlayerTypeDto(3, "Midfielder", "MID", 5, 2, 5)],
            [new FplPlayerDto(10, 100, "Test", "Player", "Test Player", 1, 3, 75, 60, 8, "7.5", "18.2", 0.45m, 0.25m, "d", "Knock", 75)]));

        public Task<IReadOnlyList<FplFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FplFixtureDto>>(
            [new FplFixtureDto(7, 700, 8, Kickoff, true, true, 1, 2, 3, 1, 2, 4)]);

        public Task<FplManagerDto> GetManagerAsync(int managerId, CancellationToken cancellationToken) => Task.FromResult(
            new FplManagerDto(managerId, "Ada", "Manager", "Expected Goals", 1, 8, 400, 1000, 60, 2000, 15, 1012));

        public Task<FplSquadPicksDto> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => Task.FromResult(new FplSquadPicksDto(
            null,
            new FplEntryHistoryDto(gameweek, 60, 400, 1000, 15, 1012, 1, 0, 7),
            [new FplPickDto(10, 1, 2, true, false, 3, 70, 74)]));

        public Task<FplPlayerSummaryDto> GetPlayerSummaryAsync(int playerId, CancellationToken cancellationToken) => Task.FromResult(new FplPlayerSummaryDto(
            [new FplPlayerFixtureDto(9, 9, "Gameweek 9", Kickoff.AddDays(7), false, 2, 1, 4)],
            [new FplPlayerGameweekHistoryDto(playerId, 7, 2, 8, true, Kickoff, 12, 90, 1, 2, 0, 1, 3, 45, 75, 2000000, 10000, 5000)],
            [new FplPlayerSeasonHistoryDto("2025/26", 100, 65, 75, 180, 2900, 12, 10, 8, 30, 20, 500)]));
    }
}
