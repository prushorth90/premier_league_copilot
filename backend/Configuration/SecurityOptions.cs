namespace Backend.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public bool UseHttpsRedirection { get; init; }

    public int RequestLimitPerMinute { get; init; } = 120;

    public int MaxRequestBodyKilobytes { get; init; } = 64;
}