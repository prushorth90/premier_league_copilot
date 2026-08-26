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

        return services;
    }
}