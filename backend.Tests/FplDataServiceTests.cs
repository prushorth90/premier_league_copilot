using Backend.Configuration;
using Backend.ExternalClients;
using Backend.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

public class FplDataServiceTests
{
    [Fact]
    public async Task GetBootstrapDataAsyncUsesCachedResultAfterFirstRequest()
    {
        var apiClient = new CountingFplApiClient();
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()),
            NullLoggerFactory.Instance);
        var service = new FplDataService(
            apiClient,
            cache,
            Options.Create(new FplApiOptions()),
            NullLogger<FplDataService>.Instance);

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

    private sealed class CountingFplApiClient : IFplApiClient
    {
        public int BootstrapRequestCount { get; private set; }

        public Task<FplBootstrapDto> GetBootstrapStaticAsync(CancellationToken cancellationToken)
        {
            BootstrapRequestCount++;
            return Task.FromResult(new FplBootstrapDto(
                [],
                [],
                [],
                [new FplPlayerDto(1, 100, "Test", "Player", "Test Player", 1, 3, 50, 0, 0, "6.5", "12.3", 0.4m, 0.2m, "a", "", null)]));
        }

        public Task<IReadOnlyList<FplFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FplManagerDto> GetManagerAsync(int managerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FplSquadPicksDto> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FplPlayerSummaryDto> GetPlayerSummaryAsync(int playerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}