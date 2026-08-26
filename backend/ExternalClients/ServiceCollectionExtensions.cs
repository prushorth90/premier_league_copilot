using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.ExternalClients;

public static class ServiceCollectionExtensions
{
    public const string FplApiClientName = "FplApi";

    public static IServiceCollection AddExternalClients(this IServiceCollection services)
    {
        services.AddHttpClient(FplApiClientName, (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FplApiOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl);
        });

        return services;
    }
}