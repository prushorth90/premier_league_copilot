using System.Text.Json;
using System.Globalization;
using Backend.Configuration;
using Backend.ExternalClients;
using Backend.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class FplDataService(
    IFplApiClient fplApiClient,
    IDistributedCache cache,
    IOptions<FplApiOptions> options,
    ILogger<FplDataService> logger) : IFplDataService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly FplApiOptions fplOptions = options.Value;

    public Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            "bootstrap-static:v2",
            TimeSpan.FromMinutes(fplOptions.BootstrapCacheMinutes),
            async token => Map(await fplApiClient.GetBootstrapStaticAsync(token)),
            cancellationToken);

    public Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync<IReadOnlyList<Fixture>>(
            "fixtures",
            TimeSpan.FromMinutes(fplOptions.FixturesCacheMinutes),
            async token => (await fplApiClient.GetFixturesAsync(token)).Select(Map).ToArray(),
            cancellationToken);

    public Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"manager:{managerId}",
            TimeSpan.FromMinutes(fplOptions.ManagerCacheMinutes),
            async token => Map(await fplApiClient.GetManagerAsync(managerId, token)),
            cancellationToken);

    public Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"manager:{managerId}:gameweek:{gameweek}:picks",
            TimeSpan.FromMinutes(fplOptions.SquadCacheMinutes),
            async token => Map(await fplApiClient.GetManagerPicksAsync(managerId, gameweek, token)),
            cancellationToken);

    public Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"player:{playerId}:history",
            TimeSpan.FromMinutes(fplOptions.PlayerHistoryCacheMinutes),
            async token => Map(await fplApiClient.GetPlayerSummaryAsync(playerId, token)),
            cancellationToken);

    private async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        TimeSpan cacheDuration,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        try
        {
            var cachedJson = await cache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedJson is not null)
            {
                var cachedValue = JsonSerializer.Deserialize<T>(cachedJson, SerializerOptions);
                if (cachedValue is not null)
                {
                    logger.LogDebug("FPL cache hit for {CacheKey}", cacheKey);
                    return cachedValue;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to read FPL cache key {CacheKey}; requesting upstream data", cacheKey);
        }

        logger.LogDebug("FPL cache miss for {CacheKey}", cacheKey);
        var value = await factory(cancellationToken);

        try
        {
            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(value, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheDuration },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to write FPL cache key {CacheKey}", cacheKey);
        }

        return value;
    }

    private static BootstrapData Map(FplBootstrapDto source) => new(
        source.Events.Select(item => new Gameweek(item.Id, item.Name, item.DeadlineTime, item.Finished, item.IsCurrent, item.IsNext, item.AverageEntryScore, item.HighestScore)).ToArray(),
        source.Teams.Select(item => new Team(item.Id, item.Code, item.Name, item.ShortName, item.Strength, item.StrengthOverallHome, item.StrengthOverallAway)).ToArray(),
        source.ElementTypes.Select(item => new PlayerPosition(item.Id, item.SingularName, item.SingularNameShort, item.SquadSelect, item.SquadMinPlay, item.SquadMaxPlay)).ToArray(),
        source.Elements.Select(item => new Player(item.Id, item.Code, item.FirstName, item.SecondName, item.WebName, item.Team, item.ElementType, item.NowCost, item.TotalPoints, item.EventPoints, ParseDecimal(item.Form), ParseDecimal(item.SelectedByPercent), item.Status, item.News, item.ChanceOfPlayingNextRound)).ToArray());

    private static Fixture Map(FplFixtureDto item) => new(item.Id, item.Code, item.Event, item.KickoffTime, item.Finished, item.Started, item.TeamH, item.TeamA, item.TeamHScore, item.TeamAScore, item.TeamHDifficulty, item.TeamADifficulty);

    private static Manager Map(FplManagerDto item) => new(item.Id, item.PlayerFirstName, item.PlayerLastName, item.Name, item.StartedEvent, item.CurrentEvent, item.SummaryOverallPoints, item.SummaryOverallRank, item.SummaryEventPoints, item.SummaryEventRank, item.LastDeadlineBank, item.LastDeadlineValue);

    private static Squad Map(FplSquadPicksDto source) => new(
        source.ActiveChip,
        new SquadGameweekSummary(source.EntryHistory.Event, source.EntryHistory.Points, source.EntryHistory.TotalPoints, source.EntryHistory.OverallRank, source.EntryHistory.Bank, source.EntryHistory.Value, source.EntryHistory.EventTransfers, source.EntryHistory.EventTransfersCost, source.EntryHistory.PointsOnBench),
        source.Picks.Select(item => new SquadPick(item.Element, item.Position, item.Multiplier, item.IsCaptain, item.IsViceCaptain, item.ElementType)).ToArray());

    private static PlayerHistory Map(FplPlayerSummaryDto source) => new(
        source.Fixtures.Select(item => new PlayerFixture(item.Id, item.Event, item.EventName, item.KickoffTime, item.IsHome, item.TeamH, item.TeamA, item.Difficulty)).ToArray(),
        source.History.Select(item => new PlayerGameweekHistory(item.Element, item.Fixture, item.OpponentTeam, item.Round, item.WasHome, item.KickoffTime, item.TotalPoints, item.Minutes, item.GoalsScored, item.Assists, item.CleanSheets, item.GoalsConceded, item.Bonus, item.Bps, item.Value, item.Selected, item.TransfersIn, item.TransfersOut)).ToArray(),
        source.HistoryPast.Select(item => new PlayerSeasonHistory(item.SeasonName, item.ElementCode, item.StartCost, item.EndCost, item.TotalPoints, item.Minutes, item.GoalsScored, item.Assists, item.CleanSheets, item.GoalsConceded, item.Bonus, item.Bps)).ToArray());

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : 0m;
}