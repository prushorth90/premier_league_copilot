using Backend.Models;

namespace Backend.Services;

public sealed class HealthStatusService : IHealthStatusService
{
    public ValueTask<HealthStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new HealthStatus("Healthy", DateTimeOffset.UtcNow));
    }
}