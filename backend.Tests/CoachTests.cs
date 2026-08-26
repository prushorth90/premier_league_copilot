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
    [InlineData("Show Saka fixtures", CoachRecommendationType.Fixture)]
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
            new StubPlayerRecommendationService(),
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
            Assert.Equal(
                [FplCoachAgents.InjurySpecialistName, FplCoachAgents.FixtureSpecialistName, FplCoachAgents.TransferSpecialistName],
                copilotClient.Grounding?.InvokedAgents);
            Assert.Equal(PlayerRecommendationAction.Transfer, copilotClient.Grounding?.Recommendation?.Action);
        }
        if (expectedType is CoachRecommendationType.Transfer or CoachRecommendationType.Replacement)
        {
            Assert.Equal(80m, response.Confidence);
            Assert.Equal(PlayerRecommendationAction.Transfer, response.Recommendation?.Action);
            Assert.Equal(8m, response.Recommendation?.ProjectedImpact);
            Assert.StartsWith("Deterministic recommendation: TRANSFER.", response.Message);
            Assert.DoesNotContain("does not confirm that Saka is injured", response.Message);
            Assert.Equal(10.5m, response.Transfers?.MaximumPurchasePrice);
            var candidate = Assert.Single(response.Transfers!.Candidates);
            Assert.Equal("Palmer", candidate.Player.PlayerName);
            Assert.Equal(8m, candidate.ProjectedPointDifference);
            Assert.Equal("Saka", response.StructuredRecommendation?.DetectedPlayer.PlayerName);
            Assert.Equal(PlayerRecommendationAction.Transfer, response.StructuredRecommendation?.RecommendedAction);
            Assert.Equal(80m, response.StructuredRecommendation?.Confidence);
            Assert.Equal("Doubtful", response.StructuredRecommendation?.InjuryStatus.Description);
            Assert.Equal("Favorable", response.StructuredRecommendation?.UpcomingFixtureSummary.ScheduleRating);
            Assert.Equal("Palmer", response.StructuredRecommendation?.SuggestedReplacement?.PlayerName);
            Assert.Equal(8m, response.StructuredRecommendation?.ProjectedImpact);
            Assert.Equal(5, response.StructuredRecommendation?.ProjectionGameweeks);
        }
        if (expectedType == CoachRecommendationType.Fixture)
        {
            Assert.Equal([FplCoachAgents.FixtureSpecialistName], copilotClient.Grounding?.InvokedAgents);
            Assert.Null(copilotClient.Grounding?.Availability);
            Assert.Null(copilotClient.Grounding?.Transfers);
            Assert.Null(copilotClient.Grounding?.Recommendation);
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
            new StubPlayerRecommendationService(),
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

    [Fact]
    public async Task CopilotCoachServiceReportsOnlyHighLevelProgressForInjuryWorkflow()
    {
        var progress = new RecordingProgressSink();
        var service = new CopilotCoachService(
            new StubFplDataService(),
            new RecordingCopilotChatClient(),
            new StubCoachFactService(),
            new StubPlayerRecommendationService(),
            TimeProvider.System);

        await service.ReplyWithProgressAsync(42, "Saka is injured", progress, CancellationToken.None);

        Assert.Equal(
            [
                "Loading squad context",
                "Checking player availability",
                "Analyzing upcoming fixtures",
                "Comparing replacements",
                "Preparing recommendation"
            ],
            progress.Updates.Select(update => update.Message));
        Assert.All(progress.Updates, update => Assert.DoesNotContain("reason", update.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StreamChatAsyncWritesProgressThenFinalResponseAsSse()
    {
        var controller = new CoachController(new StreamingCoachService());
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.StreamChatAsync(
            new CoachChatRequest(42, "Saka is injured"),
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("text/event-stream", httpContext.Response.ContentType);
        Assert.Equal("no-cache, no-transform", httpContext.Response.Headers.CacheControl);
        Assert.Equal("no", httpContext.Response.Headers["X-Accel-Buffering"]);
        Assert.Contains("event: progress\ndata: {\"code\":\"checking-availability\",\"message\":\"Checking player availability\"}", body);
        Assert.Contains("event: progress\ndata: {\"code\":\"analyzing-fixtures\",\"message\":\"Analyzing upcoming fixtures\"}", body);
        Assert.Contains("event: progress\ndata: {\"code\":\"comparing-replacements\",\"message\":\"Comparing replacements\"}", body);
        Assert.Contains("event: complete\ndata: {\"message\":\"Final recommendation.\"", body);
        Assert.True(body.IndexOf("event: progress", StringComparison.Ordinal) < body.IndexOf("event: complete", StringComparison.Ordinal));
        Assert.DoesNotContain("chain-of-thought", body, StringComparison.OrdinalIgnoreCase);
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

    private sealed class StreamingCoachService : ICoachService
    {
        public Task<CoachChatResponse> ReplyAsync(int teamId, string message, CancellationToken cancellationToken) =>
            Task.FromResult(CreateResponse(teamId));

        public async Task<CoachChatResponse> ReplyWithProgressAsync(
            int teamId,
            string message,
            ICoachProgressSink progressSink,
            CancellationToken cancellationToken)
        {
            await progressSink.ReportAsync(new CoachProgressUpdate("checking-availability", "Checking player availability"), cancellationToken);
            await progressSink.ReportAsync(new CoachProgressUpdate("analyzing-fixtures", "Analyzing upcoming fixtures"), cancellationToken);
            await progressSink.ReportAsync(new CoachProgressUpdate("comparing-replacements", "Comparing replacements"), cancellationToken);
            return CreateResponse(teamId);
        }

        private static CoachChatResponse CreateResponse(int teamId) => new(
            "Final recommendation.",
            teamId,
            DateTimeOffset.UtcNow,
            false,
            CoachRecommendationType.Recommendation,
            80m,
            null);
    }

    private sealed class RecordingProgressSink : ICoachProgressSink
    {
        public List<CoachProgressUpdate> Updates { get; } = [];

        public ValueTask ReportAsync(CoachProgressUpdate update, CancellationToken cancellationToken)
        {
            Updates.Add(update);
            return ValueTask.CompletedTask;
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
        public CoachSpecialistGrounding? Grounding { get; private set; }

        public Task<string> GenerateAsync(
            string message,
            FplCoachContext context,
            CoachSpecialistGrounding grounding,
            CancellationToken cancellationToken)
        {
            Message = message;
            Context = context;
            Grounding = grounding;
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
            Task.FromResult(new PlayerFixtureWindowResult(
                new CoachFixturePlayer(playerId, "Saka", "Arsenal", "MID"),
                gameweeks,
                [new CoachUpcomingFixture(1, 9, "Gameweek 9", null, "Chelsea", true, "Home", 2)],
                2m,
                4m,
                "Favorable",
                "Favorable schedule.",
                "Official FPL fixture data"));

        public Task<PlayerReplacementResult> GetTransferCandidatesAsync(FplCoachContext context, int playerId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new PlayerReplacementResult(
                new CoachTransferPlayer(playerId, "Saka", "Arsenal", "MID", 10m),
                0.5m,
                10.5m,
                5,
                [new CoachReplacementCandidate(
                    1,
                    new CoachTransferPlayer(20, "Palmer", "Chelsea", "MID", 9.5m),
                    -0.5m,
                    25m,
                    33m,
                    8m,
                    80m,
                    "Adds 8 projected points over five gameweeks.")],
                "Touchline transfer recommendation engine"));
    }

    private sealed class StubPlayerRecommendationService : IPlayerRecommendationService
    {
        public Task<PlayerRecommendationResult> GetRecommendationAsync(
            FplCoachContext context,
            int playerId,
            int gameweeks,
            int candidateLimit,
            CancellationToken cancellationToken)
        {
            var availability = new PlayerAvailabilityResult(
                new CoachAvailabilityPlayer(playerId, "Saka", "Arsenal", "MID"),
                "d", "Doubtful", false, 75, null, 85m, "Knock", "Official FPL bootstrap data");
            var fixtures = new PlayerFixtureWindowResult(
                new CoachFixturePlayer(playerId, "Saka", "Arsenal", "MID"),
                gameweeks,
                [new CoachUpcomingFixture(1, 9, "Gameweek 9", null, "Chelsea", true, "Home", 2)],
                2m, 4m, "Favorable", "Favorable schedule.", "Official FPL data");
            var candidate = new CoachReplacementCandidate(
                1,
                new CoachTransferPlayer(20, "Palmer", "Chelsea", "MID", 9.5m),
                -0.5m, 25m, 33m, 8m, 80m,
                "Adds eight projected points over five gameweeks.");
            var transfers = new PlayerReplacementResult(
                new CoachTransferPlayer(playerId, "Saka", "Arsenal", "MID", 10m),
                0.5m, 10.5m, 5, [candidate], "Touchline transfer recommendation engine");
            return Task.FromResult(new PlayerRecommendationResult(
                PlayerRecommendationAction.Transfer,
                8m,
                5,
                80m,
                "Transfer to Palmer.",
                candidate,
                availability,
                fixtures,
                transfers,
                "Deterministic C# recommendation policy"));
        }

        public Task<PlayerRecommendationResult?> GetRecommendationIfAtRiskAsync(
            FplCoachContext context,
            PlayerAvailabilityResult verifiedAvailability,
            int gameweeks,
            int candidateLimit,
            CancellationToken cancellationToken) =>
            GetRecommendationAsync(context, verifiedAvailability.Player.PlayerId, gameweeks, candidateLimit, cancellationToken)
                .ContinueWith<PlayerRecommendationResult?>(task => task.Result, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
