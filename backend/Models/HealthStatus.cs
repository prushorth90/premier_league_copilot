namespace Backend.Models;

public sealed record HealthStatus(
	string Status,
	DateTimeOffset Timestamp,
	IReadOnlyDictionary<string, string> Dependencies);