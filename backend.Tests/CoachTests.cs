using Backend.Coach;
using Backend.Coach.Models;
using Backend.Controllers;
using Backend.ExternalClients;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

public class CoachTests
{
    [Theory]
    [InlineData("Saka is injured", "injury concern", CoachRecommendationType.Availability)]
    [InlineData("Should I sell Saka?", "3 and 5 gameweek projection", CoachRecommendationType.Transfer)]
    [InlineData("Who can I replace Saka with?", "same-position options", CoachRecommendationType.Replacement)]
    public async Task MockCoachServiceReturnsSquadAwareReply(
        string message,
        string expectedText,
        CoachRecommendationType expectedType)
    {
        var timestamp = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var dataService = new StubFplDataService();
        var service = new MockCoachService(dataService, new FixedTimeProvider(timestamp));

        var response = await service.ReplyAsync(42, message, CancellationToken.None);

        Assert.Equal(42, response.TeamId);
        Assert.Equal(timestamp, response.RespondedAt);
        Assert.True(response.IsMocked);
        Assert.Contains(expectedText, response.Message);
        Assert.Equal(expectedType, response.RecommendationType);
        Assert.InRange(response.Confidence, 1m, 100m);
        Assert.Equal("Saka", response.Player?.PlayerName);
        Assert.Equal("Arsenal", response.Player?.TeamName);
        Assert.Equal("MID", response.Player?.Position);
        Assert.Equal(8, dataService.RequestedGameweek);
    }

    [Fact]
    public async Task MockCoachServicePropagatesMissingFplTeam()
    {
        var service = new MockCoachService(new StubFplDataService { TeamIsMissing = true }, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<FplApiException>(() =>
            service.ReplyAsync(999, "Saka is injured", CancellationToken.None));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task ChatAsyncTrimsMessageAndReturnsServiceResponse()
    {
        var service = new RecordingCoachService();
        var controller = new CoachController(service);

        var action = await controller.ChatAsync(new CoachChatRequest(42, "  Should I sell Saka?  "), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<CoachChatResponse>(ok.Value);
        Assert.Equal("Should I sell Saka?", service.Message);
        Assert.Equal(42, service.TeamId);
        Assert.Equal("Mock reply", response.Message);
    }

    [Theory]
    [InlineData(0, "Question")]
    [InlineData(42, "")]
    [InlineData(42, "   ")]
    public async Task ChatAsyncRejectsInvalidInputs(int teamId, string message)
    {
        var controller = new CoachController(new RecordingCoachService());

        var action = await controller.ChatAsync(new CoachChatRequest(teamId, message), CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task ChatAsyncRejectsOversizedMessage()
    {
        var controller = new CoachController(new RecordingCoachService());

        var action = await controller.ChatAsync(new CoachChatRequest(42, new string('a', 1_001)), CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    private sealed class RecordingCoachService : ICoachService
    {
        public int TeamId { get; private set; }
        public string? Message { get; private set; }

        public Task<CoachChatResponse> ReplyAsync(int teamId, string message, CancellationToken cancellationToken)
        {
            TeamId = teamId;
            Message = message;
            return Task.FromResult(new CoachChatResponse(
                "Mock reply",
                teamId,
                DateTimeOffset.UtcNow,
                true,
                CoachRecommendationType.General,
                35m,
                null));
        }
    }

    private sealed class StubFplDataService : IFplDataService
    {
        public bool TeamIsMissing { get; init; }

        public int? RequestedGameweek { get; private set; }

        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) => Task.FromResult(new BootstrapData(
            [],
            [new Team(1, 3, "Arsenal", "ARS", 4, 4, 4)],
            [new PlayerPosition(3, "Midfielder", "MID", 5, 2, 5)],
            [new Player(10, 223340, "Bukayo", "Saka", "Saka", 1, 3, 100, 0, 0, 5m, 30m, 0.4m, 0.2m, "d", "Knock", 75)]));

        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => TeamIsMissing
            ? throw new FplApiException($"entry/{managerId}/", System.Net.HttpStatusCode.NotFound)
            : Task.FromResult(new Manager(managerId, "Ada", "Manager", "Expected Goals", 1, 8, 400, 1000, 60, 2000, 5, 1000));

        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken)
        {
            RequestedGameweek = gameweek;
            return Task.FromResult(new Squad(
                null,
                new SquadGameweekSummary(gameweek, 60, 400, 1000, 5, 1000, 0, 0, 5),
                [new SquadPick(10, 1, 1, false, false, 3)]));
        }

        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
