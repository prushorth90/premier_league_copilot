using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services.Caching;

public sealed record FplCachePolicy(string Key, TimeSpan Duration);

public interface IFplCachePolicyProvider
{
    FplCachePolicy Bootstrap { get; }

    FplCachePolicy Fixtures { get; }

    FplCachePolicy Manager(int managerId);

    FplCachePolicy ManagerPicks(int managerId, int gameweek);

    FplCachePolicy PlayerHistory(int playerId);
}

public sealed class FplCachePolicyProvider(IOptions<FplApiOptions> options) : IFplCachePolicyProvider
{
    private const string Namespace = "api:v1";
    private readonly FplApiOptions fplOptions = options.Value;

    public FplCachePolicy Bootstrap => new(
        $"{Namespace}:bootstrap:v4",
        TimeSpan.FromMinutes(fplOptions.BootstrapCacheMinutes));

    public FplCachePolicy Fixtures => new(
        $"{Namespace}:fixtures:v1",
        TimeSpan.FromMinutes(fplOptions.FixturesCacheMinutes));

    public FplCachePolicy Manager(int managerId) => new(
        $"{Namespace}:manager:{managerId}:v1",
        TimeSpan.FromMinutes(fplOptions.ManagerCacheMinutes));

    public FplCachePolicy ManagerPicks(int managerId, int gameweek) => new(
        $"{Namespace}:manager:{managerId}:gameweek:{gameweek}:picks:v3",
        TimeSpan.FromMinutes(fplOptions.SquadCacheMinutes));

    public FplCachePolicy PlayerHistory(int playerId) => new(
        $"{Namespace}:player:{playerId}:history:v1",
        TimeSpan.FromMinutes(fplOptions.PlayerHistoryCacheMinutes));
}