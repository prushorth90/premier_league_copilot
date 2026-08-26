using System.Text.Json;
using Backend.Persistence;

namespace Backend.DTOs;

public sealed record RecommendationHistoryResponse(
    string Kind,
    JsonElement Recommendation,
    DateTimeOffset CalculatedAt,
    DateTimeOffset RecordedAt)
{
    public static RecommendationHistoryResponse From(StoredRecommendation stored) => new(
        stored.Kind,
        JsonSerializer.Deserialize<JsonElement>(stored.PayloadJson),
        stored.CalculatedAt,
        stored.RecordedAt);
}