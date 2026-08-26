namespace Backend.Configuration;

public sealed class FplApiOptions
{
    public const string SectionName = "FplApi";

    public string BaseUrl { get; init; } = string.Empty;
}