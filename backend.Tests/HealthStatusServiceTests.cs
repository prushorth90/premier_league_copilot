using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

public class HealthStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsyncReturnsHealthyWhenDependenciesRespond()
    {
        await using var dbContext = CreateDbContext();
        var timestamp = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()), NullLoggerFactory.Instance);
        var service = new HealthStatusService(dbContext, cache, new FixedTimeProvider(timestamp), NullLogger<HealthStatusService>.Instance);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal("Healthy", result.Status);
        Assert.Equal(timestamp, result.Timestamp);
        Assert.Equal("Healthy", result.Dependencies["postgresql"]);
        Assert.Equal("Healthy", result.Dependencies["redis"]);
    }

    [Fact]
    public async Task GetStatusAsyncReturnsDegradedWhenRedisIsUnavailable()
    {
        await using var dbContext = CreateDbContext();
        var service = new HealthStatusService(dbContext, new ThrowingDistributedCache(), TimeProvider.System, NullLogger<HealthStatusService>.Instance);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal("Degraded", result.Status);
        Assert.Equal("Healthy", result.Dependencies["postgresql"]);
        Assert.Equal("Degraded", result.Dependencies["redis"]);
    }

    [Fact]
    public async Task GetStatusAsyncThrowsWhenRequestIsCancelled()
    {
        await using var dbContext = CreateDbContext();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()), NullLoggerFactory.Instance);
        var service = new HealthStatusService(dbContext, cache, TimeProvider.System, NullLogger<HealthStatusService>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.GetStatusAsync(cancellationTokenSource.Token));
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Redis unavailable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis unavailable");
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new NotSupportedException();
    }
}
