namespace Backend.Coach;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachServices(this IServiceCollection services)
    {
        services.AddSingleton<ICopilotChatClient, GitHubCopilotChatClient>();
        services.AddScoped<ICoachService, CopilotCoachService>();
        return services;
    }
}