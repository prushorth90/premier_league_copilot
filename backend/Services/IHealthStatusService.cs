using Backend.Models;

namespace Backend.Services;

public interface IHealthStatusService
{
    ValueTask<HealthStatus> GetStatusAsync(CancellationToken cancellationToken);
}