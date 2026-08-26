using Backend.Controllers;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

public class FplControllerTests
{
    [Fact]
    public async Task GetTeamAsyncRejectsNonPositiveTeamId()
    {
        var controller = CreateController(new StubFplDataService());

        var action = await controller.GetTeamAsync(0, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Invalid FPL team ID.", problem.Title);
    }

    [Fact]
    public async Task GetTeamAsyncMapsManagerToCleanResponse()
    {
        var service = new StubFplDataService
        {
            Manager = CreateManager()
        };
        var controller = CreateController(service);

        var action = await controller.GetTeamAsync(42, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<FplTeamResponse>(result.Value);
        Assert.Equal("Ada Manager", response.ManagerName);
        Assert.Equal(1.5m, response.Bank);
        Assert.Equal(101.2m, response.TeamValue);
        Assert.Null(response.FreeTransfers);
        Assert.Equal(4, response.NextGameweek?.Id);
        Assert.Equal("Gameweek 4", response.NextGameweek?.Name);
    }

    [Fact]
    public async Task GetTeamSquadAsyncUsesCurrentGameweekAndEnrichesPicks()
    {
        var service = new StubFplDataService
        {
            Manager = CreateManager(),
            BootstrapData = CreateBootstrapData(),
            Fixtures = [new Fixture(7, 700, 4, DateTimeOffset.UtcNow, false, false, 1, 2, null, null, 2, 4)],
            Squad = new Squad(
                null,
                new SquadGameweekSummary(3, 67, 180, 1234, 15, 1012, 1, 4, 8),
                [new SquadPick(10, 1, 2, true, false, 3)])
        };
        var controller = CreateController(service);

        var action = await controller.GetTeamSquadAsync(42, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<FplSquadResponse>(result.Value);
        var pick = Assert.Single(response.Picks);
        Assert.Equal(3, service.RequestedGameweek);
        Assert.Equal("Test Player", pick.DisplayName);
        Assert.Equal("Arsenal", pick.TeamName);
        Assert.Equal("MID", pick.PositionName);
        Assert.Equal(5.5m, pick.Price);
        Assert.Equal(5, pick.GameweekPoints);
        Assert.Equal("CHE (H)", pick.NextOpponent);
        Assert.Equal("https://resources.premierleague.com/premierleague/photos/players/110x140/p100.png", pick.PhotoUrl);
    }

    [Fact]
    public async Task GetPlayersAsyncReturnsApplicationDtos()
    {
        var controller = CreateController(new StubFplDataService
        {
            BootstrapData = CreateBootstrapData(),
            Fixtures = [new Fixture(7, 700, 4, DateTimeOffset.UtcNow, false, false, 1, 2, null, null, 2, 4)]
        });

        var action = await controller.GetPlayersAsync(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var players = Assert.IsAssignableFrom<IReadOnlyList<FplPlayerResponse>>(result.Value);
        var player = Assert.Single(players);
        Assert.Equal("Arsenal", player.TeamName);
        Assert.Equal("MID", player.Position);
        Assert.Equal(5.5m, player.Price);
        Assert.Equal(6.5m, player.Form);
        Assert.Equal(12.3m, player.OwnershipPercentage);
        Assert.Equal("CHE (H)", player.UpcomingFixture);
        Assert.Equal("https://resources.premierleague.com/premierleague/photos/players/110x140/p100.png", player.PhotoUrl);
    }

    [Fact]
    public async Task GetFixturesAsyncReturnsTeamNames()
    {
        var service = new StubFplDataService
        {
            BootstrapData = CreateBootstrapData(),
            Fixtures = [new Fixture(7, 700, 3, DateTimeOffset.UtcNow, false, false, 1, 2, null, null, 2, 4)]
        };
        var controller = CreateController(service);

        var action = await controller.GetFixturesAsync(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var fixtures = Assert.IsAssignableFrom<IReadOnlyList<FplFixtureResponse>>(result.Value);
        var fixture = Assert.Single(fixtures);
        Assert.Equal("Arsenal", fixture.HomeTeam);
        Assert.Equal("Chelsea", fixture.AwayTeam);
    }

    private static FplController CreateController(IFplDataService service) => new(
        service,
        NullLogger<FplController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        }
    };

    private static Manager CreateManager() => new(
        42,
        "Ada",
        "Manager",
        "Expected Goals",
        1,
        3,
        180,
        1234,
        67,
        456,
        15,
        1012);

    private static BootstrapData CreateBootstrapData() => new(
        [new Gameweek(4, "Gameweek 4", DateTimeOffset.Parse("2026-09-12T12:30:00Z"), false, false, true, 0, null)],
        [
            new Team(1, 3, "Arsenal", "ARS", 4, 4, 5),
            new Team(2, 8, "Chelsea", "CHE", 4, 4, 4)
        ],
        [new PlayerPosition(3, "Midfielder", "MID", 5, 2, 5)],
        [new Player(10, 100, "Test", "Player", "Test Player", 1, 3, 55, 20, 5, 6.5m, 12.3m, 0.4m, 0.2m, "a", "", null)]);

    private sealed class StubFplDataService : IFplDataService
    {
        public BootstrapData BootstrapData { get; init; } = CreateBootstrapData();

        public IReadOnlyList<Fixture> Fixtures { get; init; } = [];

        public Manager Manager { get; init; } = CreateManager();

        public Squad Squad { get; init; } = new(
            null,
            new SquadGameweekSummary(3, 0, 0, null, 0, 1000, 0, 0, 0),
            []);

        public int? RequestedGameweek { get; private set; }

        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) =>
            Task.FromResult(BootstrapData);

        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Fixtures);

        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) =>
            Task.FromResult(Manager);

        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken)
        {
            RequestedGameweek = gameweek;
            return Task.FromResult(Squad);
        }

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}