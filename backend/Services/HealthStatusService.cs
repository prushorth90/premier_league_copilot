using Backend.Models;
using Backend.Persistence;
using Microsoft.Extensions.Caching.Distributed;

namespace Backend.Services;

public sealed class HealthStatusService(
    ApplicationDbContext dbContext,
    IDistributedCache distributedCache,
    TimeProvider timeProvider,
    ILogger<HealthStatusService> logger) : IHealthStatusService
{
    public async ValueTask<HealthStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var postgresHealthy = await CheckPostgresAsync(cancellationToken);
        var redisHealthy = await CheckRedisAsync(cancellationToken);
        var status = !postgresHealthy ? "Unhealthy" : redisHealthy ? "Healthy" : "Degraded";

        return new HealthStatus(
            status,
            timeProvider.GetUtcNow(),
            new Dictionary<string, string>
            {
                ["postgresql"] = postgresHealthy ? "Healthy" : "Unhealthy",
                ["redis"] = redisHealthy ? "Healthy" : "Degraded"
            });
    }

    private async Task<bool> CheckPostgresAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "PostgreSQL health check failed");
            return false;
        }
    }

    private async Task<bool> CheckRedisAsync(CancellationToken cancellationToken)
    {
        try
        {
            await distributedCache.GetAsync("health:redis", cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis health check failed; memory fallback remains available");
            return false;
        }
    }
}