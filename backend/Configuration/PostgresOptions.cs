namespace Backend.Configuration;

public sealed class PostgresOptions
{
    public const string SectionName = "ConnectionStrings";

    public string PostgreSQL { get; init; } = string.Empty;
}