using System.Text.Json;
using Backend.Configuration;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task ProfileRepositoryPersistsSelectedTeamAndTypedSettings()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var repository = new ProfileRepository(dbContext, timeProvider);

        var profile = await repository.CreateAsync("  Local Manager  ", 7558250, CancellationToken.None);
        var settingValue = JsonSerializer.Deserialize<JsonElement>("{\"theme\":\"light\",\"compact\":true}");
        Assert.True(await repository.SetSettingAsync(profile.Id, "display", settingValue, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        var updated = await repository.UpdateSelectedTeamAsync(profile.Id, 42, CancellationToken.None);
        var storedSetting = await repository.GetSettingAsync(profile.Id, "display", CancellationToken.None);

        Assert.Equal("Local Manager", profile.DisplayName);
        Assert.Equal(42, updated?.SelectedFplTeamId);
        Assert.Equal(timeProvider.GetUtcNow(), updated?.UpdatedAt);
        Assert.Equal("light", storedSetting?.GetProperty("theme").GetString());
        Assert.True(storedSetting?.GetProperty("compact").GetBoolean());
        Assert.Single(await repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProfileRepositoryReturnsFalseWhenSettingProfileDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var repository = new ProfileRepository(dbContext, TimeProvider.System);

        var updated = await repository.SetSettingAsync(
            Guid.NewGuid(),
            "display",
            JsonSerializer.Deserialize<JsonElement>("true"),
            CancellationToken.None);

        Assert.False(updated);
    }

    [Fact]
    public async Task RecommendationStoreReplacesSnapshotAndAppendsHistory()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var store = CreateStore(dbContext, timeProvider);

        await store.StoreAsync(42, "captain", new TestRecommendation("First", 7.5m), timeProvider.GetUtcNow(), CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await store.StoreAsync(42, "captain", new TestRecommendation("Second", 8.2m), timeProvider.GetUtcNow(), CancellationToken.None);

        var current = await store.GetCurrentAsync<TestRecommendation>(42, "captain", CancellationToken.None);
        var history = await store.GetHistoryAsync(42, "captain", 10, CancellationToken.None);

        Assert.Equal("Second", current?.Name);
        Assert.Equal(2, history.Count);
        Assert.Equal("Second", JsonSerializer.Deserialize<TestRecommendation>(history[0].PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.Name);
        Assert.Single(await dbContext.RecommendationSnapshots.ToArrayAsync());
        Assert.Equal(2, await dbContext.RecommendationHistory.CountAsync());
    }

    [Fact]
    public async Task RecommendationStoreDoesNotReturnExpiredSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var store = CreateStore(dbContext, timeProvider);
        await store.StoreAsync(42, "lineup", new TestRecommendation("Current", 70m), timeProvider.GetUtcNow(), CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(16));
        var current = await store.GetCurrentAsync<TestRecommendation>(42, "lineup", CancellationToken.None);

        Assert.Null(current);
        Assert.Single(await store.GetHistoryAsync(42, null, 10, CancellationToken.None));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static RecommendationStore CreateStore(ApplicationDbContext dbContext, TimeProvider timeProvider) => new(
        dbContext,
        Options.Create(new PersistenceOptions { RecommendationSnapshotMinutes = 15 }),
        timeProvider);

    private sealed record TestRecommendation(string Name, decimal Score);

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
