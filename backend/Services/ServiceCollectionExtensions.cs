using Backend.Services.Caching;

namespace Backend.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IHealthStatusService, HealthStatusService>();
        services.AddMemoryCache();
        services.AddSingleton<IFplCachePolicyProvider, FplCachePolicyProvider>();
        services.AddSingleton<IFplCacheCoordinator, FplCacheCoordinator>();
        services.AddScoped<IFplDataService, FplDataService>();

        return services;
    }
}