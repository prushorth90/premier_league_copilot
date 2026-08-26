using Backend.Services;

namespace Backend.Tests;

public class HealthStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsyncReturnsHealthyStatusWithCurrentUtcTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var service = new HealthStatusService();

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal("Healthy", result.Status);
        Assert.InRange(result.Timestamp, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, result.Timestamp.Offset);
    }

    [Fact]
    public async Task GetStatusAsyncThrowsWhenRequestIsCancelled()
    {
        var service = new HealthStatusService();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.GetStatusAsync(cancellationTokenSource.Token));
    }
}