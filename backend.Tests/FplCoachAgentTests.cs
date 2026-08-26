using Backend.Coach;
using Backend.Coach.Models;
using Backend.Models;
using Backend.Recommendation.Transfer;
using Backend.Recommendation.Transfer.Models;
using Backend.Services;

namespace Backend.Tests;

public class FplCoachAgentTests
{
    [Fact]
    public void AgentDefinitionsUseParentAndIsolatedSpecialistTools()
    {
        var agents = CreateAgentProvider().GetAgents();

        var parent = Assert.Single(agents, agent => agent.Name == "FplCoachAgent");
        Assert.Equal(["task"], parent.Tools);
        Assert.False(parent.Infer);
        Assert.Contains("Never invent injuries", parent.Prompt);
        Assert.Contains("You are **FplCoachAgent**", parent.Prompt);

        var injury = Assert.Single(agents, agent => agent.Name == FplCoachAgents.InjurySpecialistName);
        Assert.Equal("InjuryAgent", injury.Name);
        Assert.Equal([FplCoachAgents.AvailabilityTool], injury.Tools);
        Assert.True(injury.Infer);
        Assert.Contains("Official FPL data does not confirm", injury.Prompt);
        Assert.Contains("expected minutes are unavailable", injury.Prompt);
        Assert.Contains("Do not analyze fixtures", injury.Prompt);
        var fixture = Assert.Single(agents, agent => agent.Name == FplCoachAgents.FixtureSpecialistName);
        Assert.Equal("FixtureAgent", fixture.Name);
        Assert.Equal([FplCoachAgents.FixturesTool], fixture.Tools);
        Assert.Contains("Do not recommend", fixture.Prompt);
        var transfer = Assert.Single(agents, agent => agent.Name == FplCoachAgents.TransferSpecialistName);
        Assert.Equal("TransferAgent", transfer.Name);
        Assert.Equal([FplCoachAgents.TransfersTool], transfer.Tools);
        Assert.Contains("maximum-three-per-club", transfer.Prompt);
        Assert.Contains("projected-point difference", transfer.Prompt);
        Assert.Equal(4, agents.Count);
        Assert.Contains("invoke InjuryAgent first", parent.Prompt);
        Assert.Contains("may run concurrently", parent.Prompt);
    }

    [Fact]
    public void SessionFactorySelectsParentAndRegistersFactTools()
    {
        var factService = new FplCoachFactService(new StubFplDataService(), new StubTransferService());
        var factory = new FplCoachSessionFactory(factService, CreateAgentProvider());

        var config = factory.Create(CreateContext(), "auto", CancellationToken.None);

        Assert.Equal(FplCoachAgents.ParentName, config.Agent);
        Assert.Equal("auto", config.Model);
        Assert.Equal(4, config.CustomAgents?.Count);
        Assert.Equal(
            [FplCoachAgents.AvailabilityTool, FplCoachAgents.TransfersTool, FplCoachAgents.FixturesTool],
            config.Tools?.Select(tool => tool.Name).Order());
        Assert.Contains("builtin:task", config.AvailableTools!);
        Assert.Contains($"custom:{FplCoachAgents.AvailabilityTool}", config.AvailableTools!);
        Assert.NotNull(config.OnPermissionRequest);
    }

    [Fact]
    public void MarkdownAgentProviderFailsClosedForMissingFileOrBroadenedTools()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fpl-agents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var path in Directory.GetFiles(AgentDirectory(), "*.agent.md"))
            {
                File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
            }

            File.Delete(Path.Combine(directory, "injury.agent.md"));
            Assert.Throws<InvalidOperationException>(() => new MarkdownFplCoachAgentProvider(directory));

            File.Copy(
                Path.Combine(AgentDirectory(), "injury.agent.md"),
                Path.Combine(directory, "injury.agent.md"));
            var transferPath = Path.Combine(directory, "transfer.agent.md");
            File.WriteAllText(
                transferPath,
                File.ReadAllText(transferPath).Replace(
                    "tools: [get_transfer_candidates]",
                    "tools: [get_transfer_candidates, execute]",
                    StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => new MarkdownFplCoachAgentProvider(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AvailabilityToolReturnsOnlyOwnedPlayerFacts()
    {
        var service = new FplCoachFactService(new StubFplDataService(), new StubTransferService());
        var context = CreateContext();

        var owned = service.GetPlayerAvailability(context, 10);

        Assert.Equal("Saka", owned.Player.PlayerName);
        Assert.Equal("d", owned.Status);
        Assert.Equal("Doubtful", owned.StatusDescription);
        Assert.False(owned.IsAvailable);
        Assert.Equal(75, owned.ChanceOfPlayingNextRound);
        Assert.Equal("12 Sep", owned.ExpectedReturn);
        Assert.Equal(85m, owned.Confidence);
        Assert.Null(owned.ExpectedMinutes);
        Assert.Equal("Official FPL bootstrap data", owned.Source);
        var available = service.GetPlayerAvailability(context, 102);
        Assert.Equal("Available", available.StatusDescription);
        Assert.True(available.IsAvailable);
        Assert.Null(available.ExpectedReturn);
        Assert.Equal(95m, available.Confidence);
        Assert.Throws<KeyNotFoundException>(() => service.GetPlayerAvailability(context, 999));
    }

    [Fact]
    public async Task FixtureAndTransferToolsUseExistingBackendServices()
    {
        var dataService = new StubFplDataService();
        var transferService = new StubTransferService();
        var service = new FplCoachFactService(dataService, transferService);
        var context = CreateContext();

        var fixtures = await service.GetUpcomingFixturesAsync(context, 10, 3, CancellationToken.None);
        var transfers = await service.GetTransferCandidatesAsync(context, 10, 3, CancellationToken.None);

        Assert.Equal("Official FPL element-summary and bootstrap data", fixtures.Source);
        Assert.Equal(3, fixtures.RequestedGameweeks);
        Assert.Equal(4, fixtures.Fixtures.Count);
        Assert.Equal("Chelsea", fixtures.Fixtures[0].Opponent);
        Assert.True(fixtures.Fixtures[0].IsHome);
        Assert.Equal("Home", fixtures.Fixtures[0].Venue);
        Assert.Equal(2, fixtures.Fixtures[0].Difficulty);
        Assert.Equal(2m, fixtures.AverageDifficulty);
        Assert.Equal(4m, fixtures.AggregateScore);
        Assert.Equal("Favorable", fixtures.ScheduleRating);
        Assert.Contains("favorable", fixtures.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Saka", transfers.PlayerOut.PlayerName);
        Assert.Equal("MID", transfers.PlayerOut.Position);
        Assert.Equal(10.5m, transfers.MaximumPurchasePrice);
        Assert.Equal(0.5m, transfers.Bank);
        Assert.Equal(5, transfers.ProjectionGameweeks);
        var candidate = Assert.Single(transfers.Candidates);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("Replacement", candidate.Player.PlayerName);
        Assert.Equal("MID", candidate.Player.Position);
        Assert.Equal(-1m, candidate.PriceDifference);
        Assert.Equal(25m, candidate.PlayerOutProjectedPoints);
        Assert.Equal(35m, candidate.CandidateProjectedPoints);
        Assert.Equal(10m, candidate.ProjectedPointDifference);
        Assert.Contains("Touchline transfer recommendation engine", transfers.Source);
        Assert.Contains("three-player club rules enforced in C#", transfers.Source);
        Assert.Equal(10, dataService.RequestedPlayerId);
        Assert.Equal((42, 10, 3), transferService.ReplacementRequest);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 2 }, 1.67, 4.33, "Favorable")]
    [InlineData(new[] { 2, 3, 4 }, 3.00, 3.00, "Mixed")]
    [InlineData(new[] { 4, 5, 5 }, 4.67, 1.33, "Difficult")]
    public void FixtureDifficultyCalculatorUsesDeterministicFplScale(
        int[] difficulties,
        decimal expectedAverage,
        decimal expectedScore,
        string expectedRating)
    {
        var result = FixtureDifficultyCalculator.Calculate(difficulties);

        Assert.Equal(expectedAverage, result.AverageDifficulty);
        Assert.Equal(expectedScore, result.AggregateScore);
        Assert.Equal(expectedRating, result.ScheduleRating);
    }

    [Fact]
    public void FixtureDifficultyCalculatorHandlesEmptyAndInvalidData()
    {
        var empty = FixtureDifficultyCalculator.Calculate([]);

        Assert.Null(empty.AverageDifficulty);
        Assert.Null(empty.AggregateScore);
        Assert.Equal("Unknown", empty.ScheduleRating);
        Assert.Throws<ArgumentOutOfRangeException>(() => FixtureDifficultyCalculator.Calculate([0, 3]));
    }

    [Fact]
    public async Task FixtureToolClampsWindowToFiveGameweeksAndRejectsNonOwnedPlayer()
    {
        var service = new FplCoachFactService(new StubFplDataService(), new StubTransferService());
        var context = CreateContext();

        var result = await service.GetUpcomingFixturesAsync(context, 10, 99, CancellationToken.None);

        Assert.Equal(5, result.RequestedGameweeks);
        Assert.Equal(5, result.Fixtures.Select(fixture => fixture.Gameweek).Distinct().Count());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetUpcomingFixturesAsync(context, 999, 3, CancellationToken.None));
    }

    private static FplCoachContext CreateContext() => new(
        42,
        "Expected Goals",
        8,
        0.5m,
        100m,
        Enumerable.Range(1, 15)
            .Select(id => new FplCoachSquadPlayer(
                id == 1 ? 10 : id + 100,
                id == 1 ? "Saka" : $"Player {id}",
                "Arsenal",
                id == 1 ? "MID" : "DEF",
                5m,
                id == 1 ? "d" : "a",
                id == 1 ? "Hamstring injury. Expected back 12 Sep." : string.Empty,
                id == 1 ? 75 : null,
                id <= 11,
                false,
                false))
            .ToArray());

    private static MarkdownFplCoachAgentProvider CreateAgentProvider() => new(AgentDirectory());

    private static string AgentDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".github", "agents");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository .github/agents directory was not found.");
    }

    private sealed class StubFplDataService : IFplDataService
    {
        public int? RequestedPlayerId { get; private set; }

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken)
        {
            RequestedPlayerId = playerId;
            return Task.FromResult(new PlayerHistory(
                [
                    new PlayerFixture(1, 9, "Gameweek 9", DateTimeOffset.UtcNow.AddDays(7), true, 1, 2, 2),
                    new PlayerFixture(2, 10, "Gameweek 10", DateTimeOffset.UtcNow.AddDays(14), false, 3, 1, 3),
                    new PlayerFixture(3, 10, "Gameweek 10", DateTimeOffset.UtcNow.AddDays(17), true, 1, 4, 1),
                    new PlayerFixture(4, 11, "Gameweek 11", DateTimeOffset.UtcNow.AddDays(21), false, 5, 1, 2),
                    new PlayerFixture(5, 12, "Gameweek 12", DateTimeOffset.UtcNow.AddDays(28), true, 1, 6, 5),
                    new PlayerFixture(6, 13, "Gameweek 13", DateTimeOffset.UtcNow.AddDays(35), false, 2, 1, 4)
                ],
                [],
                []));
        }

        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) => Task.FromResult(new BootstrapData(
            [],
            [
                new Team(1, 1, "Arsenal", "ARS", 4, 4, 4),
                new Team(2, 2, "Chelsea", "CHE", 4, 4, 4),
                new Team(3, 3, "Liverpool", "LIV", 4, 4, 4),
                new Team(4, 4, "Everton", "EVE", 3, 3, 3),
                new Team(5, 5, "Newcastle", "NEW", 4, 4, 4),
                new Team(6, 6, "Man City", "MCI", 5, 5, 5)
            ],
            [],
            []));
        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubTransferService : ITransferRecommendationService
    {
        public (int TeamId, int Limit)? Request { get; private set; }
        public (int TeamId, int PlayerOutId, int Limit)? ReplacementRequest { get; private set; }

        public Task<TransferRecommendationResponse> GetRecommendationsAsync(int teamId, int limit, CancellationToken cancellationToken)
        {
            Request = (teamId, limit);
            return CreateResponse(teamId);
        }

        public Task<TransferRecommendationResponse> GetReplacementRecommendationsAsync(int teamId, int playerOutId, int limit, CancellationToken cancellationToken)
        {
            ReplacementRequest = (teamId, playerOutId, limit);
            return CreateResponse(teamId);
        }

        private static Task<TransferRecommendationResponse> CreateResponse(int teamId)
        {
            var outgoing = new TransferPlayer(10, "Saka", "Arsenal", "MID", 10m, "d", 70m, []);
            var incoming = new TransferPlayer(20, "Replacement", "Chelsea", "MID", 9m, "a", 85m, []);
            var recommendation = new TransferRecommendation(
                outgoing,
                incoming,
                -1m,
                [new TransferHorizonGain(3, 15m, 21m, 6m), new TransferHorizonGain(5, 25m, 35m, 10m)],
                2m,
                80m,
                []);
            return Task.FromResult(new TransferRecommendationResponse(
                teamId,
                8,
                DateTimeOffset.UtcNow,
                0.5m,
                [recommendation],
                []));
        }
    }
}
