namespace Backend.Coach;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachServices(this IServiceCollection services)
    {
        services.AddScoped<ICoachService, MockCoachService>();
        return services;
    }
}