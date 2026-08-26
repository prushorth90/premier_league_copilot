namespace Backend.Recommendation;

using Backend.Recommendation.Factors;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRecommendationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProjectionFactor, PositionFactor>();
        services.AddSingleton<IProjectionFactor, RecentFormFactor>();
        services.AddSingleton<IProjectionFactor, ExpectedPlayingTimeFactor>();
        services.AddSingleton<IProjectionFactor, FixtureDifficultyFactor>();
        services.AddSingleton<IProjectionFactor, HomeAwayFactor>();
        services.AddSingleton<IProjectionFactor, HistoricalPointsFactor>();
        services.AddSingleton<IProjectionFactor, AvailabilityFactor>();
        services.AddSingleton<IProjectedPointsCalculator, ProjectedPointsCalculator>();
        services.AddScoped<IProjectedPointsService, ProjectedPointsService>();

        return services;
    }
}