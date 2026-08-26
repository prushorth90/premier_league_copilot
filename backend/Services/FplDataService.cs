using System.Globalization;
using Backend.ExternalClients;
using Backend.Models;
using Backend.Services.Caching;

namespace Backend.Services;

public sealed class FplDataService(
    IFplApiClient fplApiClient,
    IFplCacheCoordinator cache,
    IFplCachePolicyProvider cachePolicies) : IFplDataService
{
    public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            cachePolicies.Bootstrap,
            async token => Map(await fplApiClient.GetBootstrapStaticAsync(token)),
            cancellationToken);

    public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync<IReadOnlyList<Fixture>>(
            cachePolicies.Fixtures,
            async token => (await fplApiClient.GetFixturesAsync(token)).Select(Map).ToArray(),
            cancellationToken);

    public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            cachePolicies.Manager(managerId),
            async token => Map(await fplApiClient.GetManagerAsync(managerId, token)),
            cancellationToken);

    public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            cachePolicies.ManagerPicks(managerId, gameweek),
            async token => Map(await fplApiClient.GetManagerPicksAsync(managerId, gameweek, token)),
            cancellationToken);

    public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            cachePolicies.PlayerHistory(playerId),
            async token => Map(await fplApiClient.GetPlayerSummaryAsync(playerId, token)),
            cancellationToken);

    private async Task<T> GetOrCreateAsync<T>(
        FplCachePolicy policy,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(policy, factory, cancellationToken);

    private static BootstrapData Map(FplBootstrapDto source) => new(
        source.Events.Select(item => new Gameweek(item.Id, item.Name, item.DeadlineTime, item.Finished, item.IsCurrent, item.IsNext, item.AverageEntryScore, item.HighestScore)).ToArray(),
        source.Teams.Select(item => new Team(item.Id, item.Code, item.Name, item.ShortName, item.Strength, item.StrengthOverallHome, item.StrengthOverallAway)).ToArray(),
        source.ElementTypes.Select(item => new PlayerPosition(item.Id, item.SingularName, item.SingularNameShort, item.SquadSelect, item.SquadMinPlay, item.SquadMaxPlay)).ToArray(),
        source.Elements.Select(item => new Player(item.Id, item.Code, item.FirstName, item.SecondName, item.WebName, item.Team, item.ElementType, item.NowCost, item.TotalPoints, item.EventPoints, ParseDecimal(item.Form), ParseDecimal(item.SelectedByPercent), item.ExpectedGoalsPer90, item.ExpectedAssistsPer90, item.Status, item.News, item.ChanceOfPlayingNextRound)).ToArray());

    private static Fixture Map(FplFixtureDto item) => new(item.Id, item.Code, item.Event, item.KickoffTime, item.Finished, item.Started, item.TeamH, item.TeamA, item.TeamHScore, item.TeamAScore, item.TeamHDifficulty, item.TeamADifficulty);

    private static Manager Map(FplManagerDto item) => new(item.Id, item.PlayerFirstName, item.PlayerLastName, item.Name, item.StartedEvent, item.CurrentEvent, item.SummaryOverallPoints, item.SummaryOverallRank, item.SummaryEventPoints, item.SummaryEventRank, item.LastDeadlineBank, item.LastDeadlineValue);

    private static Squad Map(FplSquadPicksDto source) => new(
        source.ActiveChip,
        new SquadGameweekSummary(source.EntryHistory.Event, source.EntryHistory.Points, source.EntryHistory.TotalPoints, source.EntryHistory.OverallRank, source.EntryHistory.Bank, source.EntryHistory.Value, source.EntryHistory.EventTransfers, source.EntryHistory.EventTransfersCost, source.EntryHistory.PointsOnBench),
        source.Picks.Select(item => new SquadPick(item.Element, item.Position, item.Multiplier, item.IsCaptain, item.IsViceCaptain, item.ElementType, item.PurchasePrice, item.SellingPrice)).ToArray());

    private static PlayerHistory Map(FplPlayerSummaryDto source) => new(
        source.Fixtures.Select(item => new PlayerFixture(item.Id, item.Event, item.EventName, item.KickoffTime, item.IsHome, item.TeamH, item.TeamA, item.Difficulty)).ToArray(),
        source.History.Select(item => new PlayerGameweekHistory(item.Element, item.Fixture, item.OpponentTeam, item.Round, item.WasHome, item.KickoffTime, item.TotalPoints, item.Minutes, item.GoalsScored, item.Assists, item.CleanSheets, item.GoalsConceded, item.Bonus, item.Bps, item.Value, item.Selected, item.TransfersIn, item.TransfersOut)).ToArray(),
        source.HistoryPast.Select(item => new PlayerSeasonHistory(item.SeasonName, item.ElementCode, item.StartCost, item.EndCost, item.TotalPoints, item.Minutes, item.GoalsScored, item.Assists, item.CleanSheets, item.GoalsConceded, item.Bonus, item.Bps)).ToArray());

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : 0m;
}