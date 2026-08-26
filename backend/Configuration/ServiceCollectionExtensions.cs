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

        return services;
    }
}