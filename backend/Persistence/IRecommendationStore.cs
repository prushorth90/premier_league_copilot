namespace Backend.Persistence;

public interface IRecommendationStore
{
    Task<T?> GetCurrentAsync<T>(int teamId, string kind, CancellationToken cancellationToken);

    Task StoreAsync<T>(int teamId, string kind, T recommendation, DateTimeOffset calculatedAt, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredRecommendation>> GetHistoryAsync(int teamId, string? kind, int limit, CancellationToken cancellationToken);
}

public sealed record StoredRecommendation(
    string Kind,
    string PayloadJson,
    DateTimeOffset CalculatedAt,
    DateTimeOffset RecordedAt);