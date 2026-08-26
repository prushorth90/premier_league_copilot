namespace Backend.Coach;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachServices(this IServiceCollection services)
    {
        services.AddScoped<ICopilotChatClient, GitHubCopilotChatClient>();
        services.AddScoped<IFplCoachFactService, FplCoachFactService>();
        services.AddScoped<IPlayerRecommendationService, PlayerRecommendationService>();
        services.AddSingleton<IFplCoachAgentProvider, MarkdownFplCoachAgentProvider>();
        services.AddScoped<IFplCoachSessionFactory, FplCoachSessionFactory>();
        services.AddScoped<ICoachService, CopilotCoachService>();
        return services;
    }
}