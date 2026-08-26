using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Backend.ExternalClients;

public sealed class FplApiClient(
    HttpClient httpClient,
    ILogger<FplApiClient> logger) : IFplApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public Task<FplBootstrapDto> GetBootstrapStaticAsync(CancellationToken cancellationToken) =>
        GetAsync<FplBootstrapDto>("bootstrap-static/", cancellationToken);

    public Task<IReadOnlyList<FplFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<FplFixtureDto>>("fixtures/", cancellationToken);

    public Task<FplManagerDto> GetManagerAsync(int managerId, CancellationToken cancellationToken) =>
        GetAsync<FplManagerDto>($"entry/{RequirePositive(managerId, nameof(managerId))}/", cancellationToken);

    public Task<FplSquadPicksDto> GetManagerPicksAsync(
        int managerId,
        int gameweek,
        CancellationToken cancellationToken) =>
        GetAsync<FplSquadPicksDto>(
            $"entry/{RequirePositive(managerId, nameof(managerId))}/event/{RequirePositive(gameweek, nameof(gameweek))}/picks/",
            cancellationToken);

    public Task<FplPlayerSummaryDto> GetPlayerSummaryAsync(int playerId, CancellationToken cancellationToken) =>
        GetAsync<FplPlayerSummaryDto>($"element-summary/{RequirePositive(playerId, nameof(playerId))}/", cancellationToken);

    private async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Requesting FPL endpoint {FplEndpoint}", endpoint);

        try
        {
            using var response = await httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "FPL endpoint {FplEndpoint} returned status {StatusCode} after {ElapsedMilliseconds}ms",
                    endpoint,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
                throw new FplApiException(endpoint, response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
            if (result is null)
            {
                throw new JsonException("The FPL response body was empty.");
            }

            logger.LogInformation(
                "FPL endpoint {FplEndpoint} completed in {ElapsedMilliseconds}ms",
                endpoint,
                stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("FPL endpoint {FplEndpoint} timed out", endpoint);
            throw new FplApiException(endpoint, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "FPL endpoint {FplEndpoint} was unavailable", endpoint);
            throw new FplApiException(endpoint, exception.StatusCode, exception);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "FPL endpoint {FplEndpoint} returned invalid JSON", endpoint);
            throw new FplApiException(endpoint, null, exception);
        }
    }

    private static int RequirePositive(int value, string parameterName) =>
        value > 0 ? value : throw new ArgumentOutOfRangeException(parameterName, "The identifier must be positive.");
}