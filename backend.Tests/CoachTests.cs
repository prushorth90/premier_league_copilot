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
    [InlineData("Saka is injured", CoachRecommendationType.Availability)]
    [InlineData("Should I sell Saka?", CoachRecommendationType.Transfer)]
    [InlineData("Who can I replace Saka with?", CoachRecommendationType.Replacement)]
    public async Task CopilotCoachServiceSendsStructuredSquadContext(
        string message,
        CoachRecommendationType expectedType)
    {
        var timestamp = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var dataService = new StubFplDataService();
        var copilotClient = new RecordingCopilotChatClient();
        var service = new CopilotCoachService(
            dataService,
            copilotClient,
            new StubCoachFactService(),
            new FixedTimeProvider(timestamp));

        var response = await service.ReplyAsync(42, message, CancellationToken.None);

        Assert.Equal(42, response.TeamId);
        Assert.Equal(timestamp, response.RespondedAt);
        Assert.False(response.IsMocked);
        Assert.Contains("Generated Copilot response", response.Message);
        Assert.Equal(expectedType, response.RecommendationType);
        Assert.InRange(response.Confidence, 1m, 100m);
        Assert.Equal("Saka", response.Player?.PlayerName);
        Assert.Equal("Arsenal", response.Player?.TeamName);
        Assert.Equal("MID", response.Player?.Position);
        Assert.Equal(8, dataService.RequestedGameweek);
        if (expectedType == CoachRecommendationType.Availability)
        {
            Assert.StartsWith("Official FPL data does not confirm that Saka is injured.", response.Message);
            Assert.Equal("Doubtful", response.Availability?.StatusDescription);
            Assert.Equal(85m, response.Availability?.Confidence);
        }
        Assert.Equal(message, copilotClient.Message);
        Assert.Equal(15, copilotClient.Context?.Squad.Count);
        var contextPlayer = Assert.Single(copilotClient.Context!.Squad, player => player.PlayerName == "Saka");
        Assert.True(contextPlayer.IsStarter);
    }

    [Fact]
    public async Task MockCoachServicePropagatesMissingFplTeam()
    {
        var copilotClient = new RecordingCopilotChatClient();
        var service = new CopilotCoachService(
            new StubFplDataService { TeamIsMissing = true },
            copilotClient,
            new StubCoachFactService(),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<FplApiException>(() =>
            service.ReplyAsync(999, "Saka is injured", CancellationToken.None));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Null(copilotClient.Message);
        Assert.Null(copilotClient.Context);
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
            Enumerable.Range(10, 15)
                .Select(id => id == 10
                    ? new Player(id, 223340, "Bukayo", "Saka", "Saka", 1, 3, 100, 0, 0, 5m, 30m, 0.4m, 0.2m, "d", "Knock", 75)
                    : new Player(id, id, $"Player {id}", "", $"Player {id}", 1, 3, 50, 0, 0, 1m, 1m, 0m, 0m, "a", "", null))
                .ToArray()));

        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => TeamIsMissing
            ? throw new FplApiException($"entry/{managerId}/", System.Net.HttpStatusCode.NotFound)
            : Task.FromResult(new Manager(managerId, "Ada", "Manager", "Expected Goals", 1, 8, 400, 1000, 60, 2000, 5, 1000));

        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken)
        {
            RequestedGameweek = gameweek;
            return Task.FromResult(new Squad(
                null,
                new SquadGameweekSummary(gameweek, 60, 400, 1000, 5, 1000, 0, 0, 5),
                Enumerable.Range(1, 15)
                    .Select(position => new SquadPick(position + 9, position, position <= 11 ? 1 : 0, false, false, 3))
                    .ToArray()));
        }

        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingCopilotChatClient : ICopilotChatClient
    {
        public string? Message { get; private set; }
        public FplCoachContext? Context { get; private set; }

        public Task<string> GenerateAsync(
            string message,
            FplCoachContext context,
            CancellationToken cancellationToken)
        {
            Message = message;
            Context = context;
            return Task.FromResult("Generated Copilot response");
        }
    }

    private sealed class StubCoachFactService : IFplCoachFactService
    {
        public PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId) => new(
            new CoachAvailabilityPlayer(playerId, "Saka", "Arsenal", "MID"),
            "d",
            "Doubtful",
            false,
            75,
            null,
            85m,
            "Knock",
            "Official FPL bootstrap data");

        public Task<PlayerFixtureWindowResult> GetUpcomingFixturesAsync(FplCoachContext context, int playerId, int gameweeks, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> GetTransferOptionsAsync(FplCoachContext context, string playerName, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
