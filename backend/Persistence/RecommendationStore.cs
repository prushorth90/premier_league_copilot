using System.Text.Json;
using Backend.Configuration;
using Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Persistence;

public sealed class RecommendationStore(
    ApplicationDbContext dbContext,
    IOptions<PersistenceOptions> options,
    TimeProvider timeProvider) : IRecommendationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly PersistenceOptions persistenceOptions = options.Value;

    public async Task<T?> GetCurrentAsync<T>(int teamId, string kind, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var payload = await dbContext.RecommendationSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.FplTeamId == teamId && snapshot.Kind == kind && snapshot.ExpiresAt > now)
            .Select(snapshot => snapshot.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);
        return payload is null ? default : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    public async Task StoreAsync<T>(
        int teamId,
        string kind,
        T recommendation,
        DateTimeOffset calculatedAt,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var payloadJson = JsonSerializer.Serialize(recommendation, SerializerOptions);
        var snapshot = await dbContext.RecommendationSnapshots
            .SingleOrDefaultAsync(item => item.FplTeamId == teamId && item.Kind == kind, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new RecommendationSnapshot { Id = Guid.NewGuid(), FplTeamId = teamId, Kind = kind, PayloadJson = payloadJson };
            dbContext.RecommendationSnapshots.Add(snapshot);
        }

        snapshot.PayloadJson = payloadJson;
        snapshot.CalculatedAt = calculatedAt;
        snapshot.ExpiresAt = now.AddMinutes(persistenceOptions.RecommendationSnapshotMinutes);
        snapshot.UpdatedAt = now;
        dbContext.RecommendationHistory.Add(new RecommendationHistoryEntry
        {
            FplTeamId = teamId,
            Kind = kind,
            PayloadJson = payloadJson,
            CalculatedAt = calculatedAt,
            RecordedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredRecommendation>> GetHistoryAsync(
        int teamId,
        string? kind,
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.RecommendationHistory
            .AsNoTracking()
            .Where(entry => entry.FplTeamId == teamId && (kind == null || entry.Kind == kind))
            .OrderByDescending(entry => entry.RecordedAt)
            .Take(limit)
            .Select(entry => new StoredRecommendation(entry.Kind, entry.PayloadJson, entry.CalculatedAt, entry.RecordedAt))
            .ToArrayAsync(cancellationToken);
}