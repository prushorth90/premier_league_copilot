using Backend.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Backend.ExternalClients;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExternalClients(this IServiceCollection services)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = null;
        });

        services.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IOptions<RedisOptions>>((cacheOptions, redisOptions) =>
            {
                var configuration = ConfigurationOptions.Parse(redisOptions.Value.ConnectionString);
                configuration.AbortOnConnectFail = false;
                configuration.ConnectRetry = 1;
                configuration.ConnectTimeout = 1_000;
                configuration.SyncTimeout = 1_000;
                cacheOptions.ConfigurationOptions = configuration;
                cacheOptions.InstanceName = "fpl:";
            });

        services.AddHttpClient<IFplApiClient, FplApiClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FplApiOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Touchline-FPL/1.0");
        });

        return services;
    }
}