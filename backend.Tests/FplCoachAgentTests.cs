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
        var agents = FplCoachAgents.Create();

        var parent = Assert.Single(agents, agent => agent.Name == "FplCoachAgent");
        Assert.Equal(["task"], parent.Tools);
        Assert.False(parent.Infer);
        Assert.Contains("Never invent injuries", parent.Prompt);

        var injury = Assert.Single(agents, agent => agent.Name == FplCoachAgents.InjurySpecialistName);
        Assert.Equal("InjuryAgent", injury.Name);
        Assert.Equal([FplCoachAgents.AvailabilityTool], injury.Tools);
        Assert.True(injury.Infer);
        Assert.Contains("Official FPL data does not confirm", injury.Prompt);
        var fixture = Assert.Single(agents, agent => agent.Name == FplCoachAgents.FixtureSpecialistName);
        Assert.Equal([FplCoachAgents.FixturesTool], fixture.Tools);
        var transfer = Assert.Single(agents, agent => agent.Name == FplCoachAgents.TransferSpecialistName);
        Assert.Equal([FplCoachAgents.TransfersTool], transfer.Tools);
    }

    [Fact]
    public void SessionFactorySelectsParentAndRegistersFactTools()
    {
        var factService = new FplCoachFactService(new StubFplDataService(), new StubTransferService());
        var factory = new FplCoachSessionFactory(factService);

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

        var fixtures = await service.GetUpcomingFixturesAsync(context, "Saka", 3, CancellationToken.None);
        var transfers = await service.GetTransferOptionsAsync(context, "Saka", 3, CancellationToken.None);

        Assert.Contains("Official FPL element-summary data", fixtures);
        Assert.Contains("Gameweek 9", fixtures);
        Assert.Contains("Touchline transfer recommendation engine", transfers);
        Assert.Contains("Replacement", transfers);
        Assert.Equal(10, dataService.RequestedPlayerId);
        Assert.Equal((42, 3), transferService.Request);
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

    private sealed class StubFplDataService : IFplDataService
    {
        public int? RequestedPlayerId { get; private set; }

        public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken)
        {
            RequestedPlayerId = playerId;
            return Task.FromResult(new PlayerHistory(
                [new PlayerFixture(1, 9, "Gameweek 9", DateTimeOffset.UtcNow.AddDays(7), true, 1, 2, 2)],
                [],
                []));
        }

        public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubTransferService : ITransferRecommendationService
    {
        public (int TeamId, int Limit)? Request { get; private set; }

        public Task<TransferRecommendationResponse> GetRecommendationsAsync(int teamId, int limit, CancellationToken cancellationToken)
        {
            Request = (teamId, limit);
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
