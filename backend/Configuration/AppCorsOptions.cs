namespace Backend.Configuration;

public sealed class AppCorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}