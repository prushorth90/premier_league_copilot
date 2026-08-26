namespace Backend.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IHealthStatusService, HealthStatusService>();

        return services;
    }
}