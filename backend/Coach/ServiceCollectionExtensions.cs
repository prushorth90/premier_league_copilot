namespace Backend.Coach;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachServices(this IServiceCollection services)
    {
        services.AddScoped<IFplCoachFactService, FplCoachFactService>();
        services.AddScoped<IPlayerRecommendationService, PlayerRecommendationService>();
        services.AddSingleton<IFplCoachAgentProvider, MarkdownFplCoachAgentProvider>();
        services.AddScoped<ICoachService, FplCoachService>();
        return services;
    }
}