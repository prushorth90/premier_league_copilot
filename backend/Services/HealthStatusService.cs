namespace Backend.Services;

public interface IHealthStatusService
{
    HealthStatus GetStatus();
}

public sealed class HealthStatusService : IHealthStatusService
{
    public HealthStatus GetStatus() => new("Healthy", DateTimeOffset.UtcNow);
}

public sealed record HealthStatus(string Status, DateTimeOffset Timestamp);