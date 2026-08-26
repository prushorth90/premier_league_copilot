namespace Backend.Recommendation;

using Backend.Recommendation.Factors;
using Backend.Recommendation.Captain;
using Backend.Recommendation.Captain.Factors;

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
        services.AddSingleton<ICaptainFactor, ProjectedPointsCaptainFactor>();
        services.AddSingleton<ICaptainFactor, ExpectedMinutesCaptainFactor>();
        services.AddSingleton<ICaptainFactor, FixtureQualityCaptainFactor>();
        services.AddSingleton<ICaptainFactor, AttackingPotentialCaptainFactor>();
        services.AddSingleton<ICaptainFactor, AvailabilityCaptainFactor>();
        services.AddSingleton<ICaptainRankingCalculator, CaptainRankingCalculator>();
        services.AddScoped<ICaptainRecommendationService, CaptainRecommendationService>();

        return services;
    }
}