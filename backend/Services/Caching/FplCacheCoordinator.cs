using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Services.Caching;

public sealed class FplCacheCoordinator(
    IDistributedCache distributedCache,
    IMemoryCache memoryCache,
    IHostApplicationLifetime applicationLifetime,
    ILogger<FplCacheCoordinator> logger) : IFplCacheCoordinator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Lazy<Task<object>>> inFlightRequests = new();

    public async Task<T> GetOrCreateAsync<T>(
        FplCachePolicy policy,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<T>(policy.Key, out var memoryValue) && memoryValue is not null)
        {
            logger.LogDebug("FPL memory cache hit for {CacheKey}", policy.Key);
            return memoryValue;
        }

        var request = inFlightRequests.GetOrAdd(
            policy.Key,
            _ => new Lazy<Task<object>>(
                () => FetchAndCacheAsync(policy, factory),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var value = await request.Value.WaitAsync(cancellationToken);
        return (T)value;
    }

    private async Task<object> FetchAndCacheAsync<T>(
        FplCachePolicy policy,
        Func<CancellationToken, Task<T>> factory)
    {
        try
        {
            if (memoryCache.TryGetValue<T>(policy.Key, out var memoryValue) && memoryValue is not null)
            {
                return memoryValue;
            }

            var distributedValue = await TryGetDistributedAsync<T>(policy.Key, applicationLifetime.ApplicationStopping);
            if (distributedValue is not null)
            {
                memoryCache.Set(policy.Key, distributedValue, policy.Duration);
                return distributedValue;
            }

            logger.LogDebug("FPL cache miss for {CacheKey}; requesting upstream data", policy.Key);
            var value = await factory(applicationLifetime.ApplicationStopping);
            memoryCache.Set(policy.Key, value, policy.Duration);
            await TrySetDistributedAsync(policy, value, applicationLifetime.ApplicationStopping);
            return value!;
        }
        finally
        {
            inFlightRequests.TryRemove(policy.Key, out _);
        }
    }

    private async Task<T?> TryGetDistributedAsync<T>(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cachedJson = await distributedCache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedJson is null)
            {
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(cachedJson, SerializerOptions);
            if (value is not null)
            {
                logger.LogDebug("FPL Redis cache hit for {CacheKey}", cacheKey);
                return value;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to read FPL Redis cache key {CacheKey}; using local fallback", cacheKey);
        }

        return default;
    }

    private async Task TrySetDistributedAsync<T>(
        FplCachePolicy policy,
        T value,
        CancellationToken cancellationToken)
    {
        try
        {
            await distributedCache.SetStringAsync(
                policy.Key,
                JsonSerializer.Serialize(value, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.Duration },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to write FPL Redis cache key {CacheKey}; local fallback remains active", policy.Key);
        }
    }
}