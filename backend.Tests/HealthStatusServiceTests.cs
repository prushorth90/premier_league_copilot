using Backend.Services;

namespace Backend.Tests;

public class HealthStatusServiceTests
{
    [Fact]
    public void GetStatusReturnsHealthyStatusWithCurrentUtcTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var service = new HealthStatusService();

        var result = service.GetStatus();

        Assert.Equal("Healthy", result.Status);
        Assert.InRange(result.Timestamp, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, result.Timestamp.Offset);
    }
}