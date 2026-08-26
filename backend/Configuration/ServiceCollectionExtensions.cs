namespace Backend.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FplApiOptions>()
            .Bind(configuration.GetSection(FplApiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                $"{FplApiOptions.SectionName}:BaseUrl must be an absolute URL")
            .Validate(options => options.RequestTimeoutSeconds > 0,
                $"{FplApiOptions.SectionName}:RequestTimeoutSeconds must be greater than zero")
            .Validate(options =>
                    options.BootstrapCacheMinutes > 0 &&
                    options.FixturesCacheMinutes > 0 &&
                    options.ManagerCacheMinutes > 0 &&
                    options.SquadCacheMinutes > 0 &&
                    options.PlayerHistoryCacheMinutes > 0,
                $"{FplApiOptions.SectionName} cache durations must be greater than zero")
            .ValidateOnStart();

        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.PostgreSQL),
                $"{PostgresOptions.SectionName}:PostgreSQL is required")
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"{RedisOptions.SectionName}:ConnectionString is required")
            .ValidateOnStart();

        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .Validate(options => options.RecommendationSnapshotMinutes > 0,
                $"{PersistenceOptions.SectionName}:RecommendationSnapshotMinutes must be greater than zero")
            .ValidateOnStart();

        services.AddOptions<AppCorsOptions>()
            .Bind(configuration.GetSection(AppCorsOptions.SectionName))
            .Validate(options => options.AllowedOrigins.Length > 0,
                $"{AppCorsOptions.SectionName}:AllowedOrigins must contain at least one origin")
            .Validate(options => options.AllowedOrigins.All(origin =>
                    Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                    uri.Scheme is "http" or "https" &&
                    uri.AbsolutePath == "/" &&
                    string.IsNullOrEmpty(uri.Query) &&
                    string.IsNullOrEmpty(uri.Fragment)),
                $"{AppCorsOptions.SectionName}:AllowedOrigins entries must be HTTP(S) origins without paths")
            .ValidateOnStart();

        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .Validate(options => options.RequestLimitPerMinute is >= 10 and <= 10_000,
                $"{SecurityOptions.SectionName}:RequestLimitPerMinute must be between 10 and 10000")
            .Validate(options => options.MaxRequestBodyKilobytes is >= 1 and <= 1024,
                $"{SecurityOptions.SectionName}:MaxRequestBodyKilobytes must be between 1 and 1024")
            .ValidateOnStart();

        services.AddOptions<CopilotOptions>()
            .Bind(configuration.GetSection(CopilotOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model),
                $"{CopilotOptions.SectionName}:Model is required")
            .Validate(options => options.RequestTimeoutSeconds is >= 10 and <= 300,
                $"{CopilotOptions.SectionName}:RequestTimeoutSeconds must be between 10 and 300")
            .Validate(options => Path.IsPathFullyQualified(options.BaseDirectory),
                $"{CopilotOptions.SectionName}:BaseDirectory must be an absolute path")
            .Validate(options => string.IsNullOrWhiteSpace(options.RuntimeUrl) ||
                    Uri.TryCreate(options.RuntimeUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
                $"{CopilotOptions.SectionName}:RuntimeUrl must be an absolute HTTP(S) URL")
            .Validate(options => string.IsNullOrWhiteSpace(options.RuntimeUrl) || string.IsNullOrWhiteSpace(options.GitHubToken),
                $"{CopilotOptions.SectionName}:GitHubToken cannot be combined with RuntimeUrl")
            .ValidateOnStart();

        return services;
    }
}