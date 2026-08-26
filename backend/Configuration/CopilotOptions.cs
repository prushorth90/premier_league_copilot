namespace Backend.Configuration;

public sealed class CopilotOptions
{
    public const string SectionName = "Copilot";

    public string Model { get; init; } = "auto";

    public string? GitHubToken { get; init; }

    public string? RuntimeUrl { get; init; }

    public string? RuntimeConnectionToken { get; init; }

    public string BaseDirectory { get; init; } = "/tmp/touchline-copilot";

    public int RequestTimeoutSeconds { get; init; } = 120;
}