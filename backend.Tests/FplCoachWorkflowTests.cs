using Backend.Coach;
using Backend.Coach.Models;
using Backend.Models;
using Backend.Recommendation.Models;
using Backend.Recommendation.Transfer;
using Backend.Recommendation.Transfer.Models;
using Backend.Services;

namespace Backend.Tests;

public class FplCoachWorkflowTests
{
    [Fact]
    public async Task ConfirmedInjuryProducesDeterministicTransferAndRejectsContradictoryHoldResponse()
    {
        var result = await RunWorkflowAsync(
            Availability("i", "Injured", 0),
            Fixtures("Mixed", 3),
            Transfers(Candidate(4m)),
            "You should hold Saka.",
            "Saka is injured");

        Assert.Equal(PlayerRecommendationAction.Transfer, result.Response.Recommendation?.Action);
        Assert.Equal(4m, result.Response.Recommendation?.ProjectedImpact);
        Assert.Equal(91.50m, result.Response.Recommendation?.Confidence);
        Assert.StartsWith("Deterministic recommendation: TRANSFER.", result.Response.Message);
        Assert.DoesNotContain("should hold", result.Response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            [FplCoachAgents.InjurySpecialistName, FplCoachAgents.FixtureSpecialistName, FplCoachAgents.TransferSpecialistName],
            result.Copilot.Grounding?.InvokedAgents);
    }

    [Fact]
    public async Task UnconfirmedInjuryClaimStopsAfterVerificationAndRejectsFalseInjuryResponse()
    {
        var result = await RunWorkflowAsync(
            Availability("a", "Available", 100),
            Fixtures("Difficult", 5),
            Transfers(Candidate(8m)),
            "Saka is injured.",
            "Saka is injured");

        Assert.Null(result.Response.Recommendation);
        Assert.Equal(
            "Official FPL data does not confirm that Saka is injured. Current status: Available.",
            result.Response.Message);
        Assert.Equal([FplCoachAgents.InjurySpecialistName], result.Copilot.Grounding?.InvokedAgents);
        Assert.Equal(0, result.Facts.FixtureCalls);
        Assert.Equal(0, result.Facts.TransferCalls);
    }

    [Fact]
    public async Task FavorableFixtureResultCannotBeDescribedAsDifficult()
    {
        var fixtures = Fixtures("Favorable", 2);
        var result = await RunWorkflowAsync(
            Availability("a", "Available", 100),
            fixtures,
            Transfers(),
            "Saka has a difficult upcoming schedule.",
            "Show Saka fixtures");

        Assert.Equal(fixtures.Explanation, result.Response.Message);
        Assert.Equal("Favorable", result.Response.Fixtures?.ScheduleRating);
        Assert.DoesNotContain("difficult", result.Response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([FplCoachAgents.FixtureSpecialistName], result.Copilot.Grounding?.InvokedAgents);
    }

    [Fact]
    public async Task UnaffordableReplacementIsExcludedAndProducesDeterministicBench()
    {
        var transferResult = InvalidTransferResult(incomingTeamId: 6, incomingPrice: 56, bank: 5);
        var result = await RunWorkflowAsync(
            Availability("i", "Injured", 0),
            Fixtures("Mixed", 3),
            transferResult,
            "You should transfer Saka.",
            "Saka is injured");

        Assert.Empty(transferResult.Candidates);
        Assert.Equal(PlayerRecommendationAction.Bench, result.Response.Recommendation?.Action);
        Assert.Equal(0m, result.Response.Recommendation?.ProjectedImpact);
        Assert.Equal(89.75m, result.Response.Recommendation?.Confidence);
        Assert.StartsWith("Deterministic recommendation: BENCH.", result.Response.Message);
        Assert.DoesNotContain("should transfer", result.Response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FourthPlayerFromClubIsExcludedAndProducesDeterministicBench()
    {
        var transferResult = InvalidTransferResult(incomingTeamId: 1, incomingPrice: 50, bank: 0);
        var result = await RunWorkflowAsync(
            Availability("i", "Injured", 0),
            Fixtures("Mixed", 3),
            transferResult,
            "My recommendation is to transfer Saka.",
            "Saka is injured");

        Assert.Empty(transferResult.Candidates);
        Assert.Equal(PlayerRecommendationAction.Bench, result.Response.Recommendation?.Action);
        Assert.Equal(0m, result.Response.Recommendation?.ProjectedImpact);
        Assert.Equal(89.75m, result.Response.Recommendation?.Confidence);
        Assert.StartsWith("Deterministic recommendation: BENCH.", result.Response.Message);
        Assert.DoesNotContain("recommendation is to transfer", result.Response.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<WorkflowResult> RunWorkflowAsync(
        PlayerAvailabilityResult availability,
        PlayerFixtureWindowResult fixtures,
        PlayerReplacementResult transfers,
        string modelResponse,
        string message)
    {
        var facts = new ScenarioFactService(availability, fixtures, transfers);
        var copilot = new ScenarioCopilotClient(modelResponse);
        var service = new CopilotCoachService(
            new ScenarioFplDataService(),
            copilot,
            facts,
            new PlayerRecommendationService(facts),
            TimeProvider.System);

        var response = await service.ReplyAsync(42, message, CancellationToken.None);
        return new(response, facts, copilot);
    }

    private static PlayerAvailabilityResult Availability(string status, string description, int chance) => new(
        new CoachAvailabilityPlayer(10, "Saka", "Arsenal", "MID"),
        status,
        description,
        status == "a",
        chance,
        null,
        95m,
        status == "i" ? "Confirmed injury in official FPL data." : "No current FPL injury flag.",
        "Official FPL bootstrap data");

    private static PlayerFixtureWindowResult Fixtures(string rating, int difficulty) => new(
        new CoachFixturePlayer(10, "Saka", "Arsenal", "MID"),
        5,
        [new CoachUpcomingFixture(1, 9, "Gameweek 9", null, "Chelsea", true, "Home", difficulty)],
        difficulty,
        6m - difficulty,
        rating,
        $"Saka has a {rating.ToLowerInvariant()} upcoming schedule based on verified FPL fixtures.",
        "Official FPL fixture data");

    private static PlayerReplacementResult Transfers(params CoachReplacementCandidate[] candidates) => new(
        new CoachTransferPlayer(10, "Saka", "Arsenal", "MID", 5m),
        0.5m,
        5.5m,
        5,
        candidates,
        "C#-validated transfer engine");

    private static CoachReplacementCandidate Candidate(decimal gain) => new(
        1,
        new CoachTransferPlayer(20, "Palmer", "Chelsea", "MID", 5m),
        0m,
        20m,
        20m + gain,
        gain,
        85m,
        "Deterministic projected-points upgrade.");

    private static PlayerReplacementResult InvalidTransferResult(int incomingTeamId, int incomingPrice, int bank)
    {
        var engine = new TransferRecommendationEngine();
        var recommendations = engine.RankReplacements(
            ValidSquad(),
            [TransferContext(101, incomingTeamId, 3, incomingPrice, 10m, 30m, 50m)],
            bank,
            playerOutId: 10,
            limit: 3);
        return Transfers(recommendations.Select((recommendation, index) => new CoachReplacementCandidate(
            index + 1,
            new CoachTransferPlayer(
                recommendation.PlayerIn.PlayerId,
                recommendation.PlayerIn.PlayerName,
                recommendation.PlayerIn.TeamName,
                recommendation.PlayerIn.Position,
                recommendation.PlayerIn.Price),
            recommendation.PriceDifference,
            recommendation.ExpectedPointGains.Single(gain => gain.Gameweeks == 5).PlayerOutPoints,
            recommendation.ExpectedPointGains.Single(gain => gain.Gameweeks == 5).PlayerInPoints,
            recommendation.ExpectedPointGains.Single(gain => gain.Gameweeks == 5).ExpectedPointGain,
            recommendation.ConfidenceScore,
            "Validated candidate."))
            .ToArray());
    }

    private static IReadOnlyList<TransferPlayerContext> ValidSquad() =>
    [
        TransferContext(1, 1, 1, 60, 4m, 12m, 20m, 50),
        TransferContext(2, 2, 1, 60, 4m, 12m, 20m, 50),
        TransferContext(3, 3, 2, 60, 4m, 12m, 20m, 50),
        TransferContext(4, 4, 2, 60, 4m, 12m, 20m, 50),
        TransferContext(5, 5, 2, 60, 4m, 12m, 20m, 50),
        TransferContext(6, 1, 2, 60, 4m, 12m, 20m, 50),
        TransferContext(7, 2, 2, 60, 4m, 12m, 20m, 50),
        TransferContext(8, 3, 3, 60, 4m, 12m, 20m, 50),
        TransferContext(9, 4, 3, 60, 4m, 12m, 20m, 50),
        TransferContext(10, 5, 3, 50, 4m, 12m, 20m, 50),
        TransferContext(11, 1, 3, 60, 4m, 12m, 20m, 50),
        TransferContext(12, 2, 3, 60, 4m, 12m, 20m, 50),
        TransferContext(13, 3, 4, 60, 4m, 12m, 20m, 50),
        TransferContext(14, 4, 4, 60, 4m, 12m, 20m, 50),
        TransferContext(15, 5, 4, 60, 4m, 12m, 20m, 50)
    ];

    private static TransferPlayerContext TransferContext(
        int id,
        int teamId,
        int positionId,
        int price,
        decimal oneGameweek,
        decimal threeGameweeks,
        decimal fiveGameweeks,
        int? sellingPrice = null)
    {
        var position = positionId switch { 1 => "GKP", 2 => "DEF", 3 => "MID", _ => "FWD" };
        var player = new Player(id, id, id == 10 ? "Saka" : $"Player {id}", "", id == 10 ? "Saka" : $"Player {id}", teamId, positionId, price, 0, 0, 0, 0, 0, 0, "a", "", null);
        var projection = new PlayerProjection(
            id,
            player.DisplayName,
            DateTimeOffset.UnixEpoch,
            90m,
            [Horizon(1, oneGameweek), Horizon(3, threeGameweeks), Horizon(5, fiveGameweeks)]);
        return new TransferPlayerContext(player, $"Team {teamId}", position, projection, sellingPrice);
    }

    private static ProjectionHorizon Horizon(int gameweeks, decimal points) => new(
        gameweeks,
        points,
        [new ProjectionFactorBreakdown("Fixture difficulty", 0m, "Verified fixtures")],
        [new FixtureProjection(gameweeks, gameweeks, points, [])]);

    private sealed class ScenarioFactService(
        PlayerAvailabilityResult availability,
        PlayerFixtureWindowResult fixtures,
        PlayerReplacementResult transfers) : IFplCoachFactService
    {
        public int FixtureCalls { get; private set; }
        public int TransferCalls { get; private set; }

        public PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId) => availability;

        public Task<PlayerFixtureWindowResult> GetUpcomingFixturesAsync(FplCoachContext context, int playerId, int gameweeks, CancellationToken cancellationToken)
        {
            FixtureCalls++;
            return Task.FromResult(fixtures);
        }

        public Task<PlayerReplacementResult> GetTransferCandidatesAsync(FplCoachContext context, int playerId, int limit, CancellationToken cancellationToken)
        {
            TransferCalls++;
            return Task.FromResult(transfers);
        }
    }

    private sealed class ScenarioCopilotClient(string response) : ICopilotChatClient
    {
        public CoachSpecialistGrounding? Grounding { get; private set; }

        public Task<string> GenerateAsync(string message, FplCoachContext context, CoachSpecialistGrounding grounding, CancellationToken cancellationToken)
        {
            Grounding = grounding;
            return Task.FromResult(response);
        }
    }

    private sealed class ScenarioFplDataService : IFplDataService
    {
        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) => Task.FromResult(new BootstrapData(
            [],
            [new Team(1, 1, "Arsenal", "ARS", 4, 4, 4)],
            [new PlayerPosition(3, "Midfielder", "MID", 5, 2, 5)],
            Enumerable.Range(10, 15).Select(id => new Player(
                id,
                id,
                id == 10 ? "Bukayo" : $"Player {id}",
                id == 10 ? "Saka" : "",
                id == 10 ? "Saka" : $"Player {id}",
                1,
                3,
                50,
                0,
                0,
                0,
                0,
                0,
                0,
                "a",
                "",
                null)).ToArray()));

        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => Task.FromResult(new Manager(
            managerId, "Ada", "Manager", "Expected Goals", 1, 8, 100, 1, 50, 1, 5, 1000));

        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => Task.FromResult(new Squad(
            null,
            new SquadGameweekSummary(gameweek, 50, 100, 1, 5, 1000, 0, 0, 0),
            Enumerable.Range(10, 15).Select((id, index) => new SquadPick(id, index + 1, index < 11 ? 1 : 0, false, false, 3)).ToArray()));

        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed record WorkflowResult(
        CoachChatResponse Response,
        ScenarioFactService Facts,
        ScenarioCopilotClient Copilot);
}