using Backend.Configuration;
using Backend.ExternalClients;
using Backend.Services;
using Backend.Services.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

public class FplDataServiceTests
{
    [Fact]
    public async Task GetBootstrapDataAsyncUsesCachedResultAfterFirstRequest()
    {
        var apiClient = new CountingFplApiClient();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()), NullLoggerFactory.Instance);
        var service = CreateService(apiClient, distributedCache, memoryCache);

        var first = await service.GetBootstrapDataAsync(CancellationToken.None);
        var second = await service.GetBootstrapDataAsync(CancellationToken.None);

        Assert.Equal(1, apiClient.BootstrapRequestCount);
        var firstPlayer = Assert.Single(first.Players);
        var cachedPlayer = Assert.Single(second.Players);
        Assert.Equal("Test Player", firstPlayer.DisplayName);
        Assert.Equal(6.5m, firstPlayer.Form);
        Assert.Equal(12.3m, firstPlayer.OwnershipPercentage);
        Assert.Equal(firstPlayer, cachedPlayer);
    }

    [Fact]
    public async Task ConcurrentCacheMissesShareOneUpstreamRequest()
    {
        var apiClient = new CountingFplApiClient(blockRequest: true);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new ThrowingDistributedCache();
        var service = CreateService(apiClient, distributedCache, memoryCache);
        var requests = Enumerable.Range(0, 20).Select(_ => service.GetBootstrapDataAsync(CancellationToken.None)).ToArray();

        await apiClient.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, apiClient.BootstrapRequestCount);
        apiClient.ReleaseRequest();
        await Task.WhenAll(requests);

        Assert.Equal(1, apiClient.BootstrapRequestCount);
        Assert.Equal(1, distributedCache.GetCount);
    }

    [Fact]
    public async Task CallerCancellationDoesNotCancelSharedUpstreamRequest()
    {
        var apiClient = new CountingFplApiClient(blockRequest: true);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(apiClient, new ThrowingDistributedCache(), memoryCache);
        using var cancellation = new CancellationTokenSource();
        var cancelledRequest = service.GetBootstrapDataAsync(cancellation.Token);
        var survivingRequest = service.GetBootstrapDataAsync(CancellationToken.None);

        await apiClient.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledRequest);
        apiClient.ReleaseRequest();
        var result = await survivingRequest;

        Assert.Single(result.Players);
        Assert.Equal(1, apiClient.BootstrapRequestCount);
    }

    [Fact]
    public async Task RedisFailureFallsBackToMemoryWithoutRepeatedUpstreamCalls()
    {
        var apiClient = new CountingFplApiClient();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(apiClient, new ThrowingDistributedCache(), memoryCache);

        await service.GetBootstrapDataAsync(CancellationToken.None);
        await service.GetBootstrapDataAsync(CancellationToken.None);

        Assert.Equal(1, apiClient.BootstrapRequestCount);
    }

    [Fact]
    public async Task CorruptRedisValueFallsBackToUpstreamAndRepairsLocalCache()
    {
        var apiClient = new CountingFplApiClient();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new CorruptDistributedCache();
        var service = CreateService(apiClient, distributedCache, memoryCache);

        await service.GetBootstrapDataAsync(CancellationToken.None);
        await service.GetBootstrapDataAsync(CancellationToken.None);

        Assert.Equal(1, apiClient.BootstrapRequestCount);
        Assert.Equal(1, distributedCache.SetCount);
    }

    [Fact]
    public void CachePolicyCentralizesVersionedKeysAndRelativeDurations()
    {
        var policies = new FplCachePolicyProvider(Options.Create(new FplApiOptions
        {
            BootstrapCacheMinutes = 120,
            FixturesCacheMinutes = 10,
            ManagerCacheMinutes = 2,
            SquadCacheMinutes = 3,
            PlayerHistoryCacheMinutes = 30
        }));

        Assert.Equal("api:v1:bootstrap:v4", policies.Bootstrap.Key);
        Assert.Equal(TimeSpan.FromMinutes(120), policies.Bootstrap.Duration);
        Assert.Equal("api:v1:fixtures:v1", policies.Fixtures.Key);
        Assert.Equal(TimeSpan.FromMinutes(10), policies.Fixtures.Duration);
        Assert.Equal("api:v1:manager:42:v1", policies.Manager(42).Key);
        Assert.Equal(TimeSpan.FromMinutes(2), policies.Manager(42).Duration);
        Assert.Equal("api:v1:manager:42:gameweek:8:picks:v3", policies.ManagerPicks(42, 8).Key);
        Assert.Equal(TimeSpan.FromMinutes(3), policies.ManagerPicks(42, 8).Duration);
        Assert.Equal("api:v1:player:99:history:v1", policies.PlayerHistory(99).Key);
        Assert.Equal(TimeSpan.FromMinutes(30), policies.PlayerHistory(99).Duration);
        Assert.True(policies.Bootstrap.Duration > policies.Fixtures.Duration);
        Assert.True(policies.PlayerHistory(99).Duration > policies.Manager(42).Duration);
    }

    private static FplDataService CreateService(IFplApiClient apiClient, IDistributedCache distributedCache, IMemoryCache memoryCache)
    {
        var options = Options.Create(new FplApiOptions());
        var coordinator = new FplCacheCoordinator(distributedCache, memoryCache, new TestApplicationLifetime(), NullLogger<FplCacheCoordinator>.Instance);
        return new FplDataService(apiClient, coordinator, new FplCachePolicyProvider(options));
    }

    private sealed class CountingFplApiClient(bool blockRequest = false) : IFplApiClient
    {
        private readonly TaskCompletionSource releaseRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int bootstrapRequestCount;

        public int BootstrapRequestCount => bootstrapRequestCount;
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<FplBootstrapDto> GetBootstrapStaticAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref bootstrapRequestCount);
            RequestStarted.TrySetResult();
            if (blockRequest)
            {
                await releaseRequest.Task.WaitAsync(cancellationToken);
            }

            return new FplBootstrapDto([], [], [], [new FplPlayerDto(1, 100, "Test", "Player", "Test Player", 1, 3, 50, 0, 0, "6.5", "12.3", 0.4m, 0.2m, "a", "", null)]);
        }

        public void ReleaseRequest() => releaseRequest.TrySetResult();
        public Task<IReadOnlyList<FplFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FplManagerDto> GetManagerAsync(int managerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FplSquadPicksDto> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FplPlayerSummaryDto> GetPlayerSummaryAsync(int playerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        private int getCount;

        public int GetCount => getCount;

        public byte[]? Get(string key) => throw new InvalidOperationException("Redis unavailable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) { Interlocked.Increment(ref getCount); throw new InvalidOperationException("Redis unavailable"); }
        public void Refresh(string key) => throw new InvalidOperationException("Redis unavailable");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis unavailable");
        public void Remove(string key) => throw new InvalidOperationException("Redis unavailable");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis unavailable");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("Redis unavailable");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("Redis unavailable");
    }

    private sealed class CorruptDistributedCache : IDistributedCache
    {
        public int SetCount { get; private set; }
        public byte[]? Get(string key) => "not-json"u8.ToArray();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>("not-json"u8.ToArray());
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetCount++;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { SetCount++; return Task.CompletedTask; }
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
    }
}
