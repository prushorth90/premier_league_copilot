using System.Text.Json;
using Backend.Coach.Models;
using Backend.Recommendation.Transfer;
using Backend.Services;

namespace Backend.Coach;

public sealed class FplCoachFactService(
    IFplDataService fplDataService,
    ITransferRecommendationService transferRecommendationService) : IFplCoachFactService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string GetPlayerAvailability(FplCoachContext context, string playerName)
    {
        var player = FindOwnedPlayer(context, playerName);
        return player is null
            ? NotFound(playerName)
            : Serialize(new
            {
                player.PlayerId,
                player.PlayerName,
                player.TeamName,
                player.Position,
                player.Status,
                player.ChanceOfPlayingNextRound,
                source = "Official FPL bootstrap data"
            });
    }

    public async Task<string> GetUpcomingFixturesAsync(
        FplCoachContext context,
        string playerName,
        int gameweeks,
        CancellationToken cancellationToken)
    {
        var player = FindOwnedPlayer(context, playerName);
        if (player is null)
        {
            return NotFound(playerName);
        }

        var history = await fplDataService.GetPlayerHistoryAsync(player.PlayerId, cancellationToken);
        var fixtures = history.Fixtures
            .Where(fixture => fixture.Gameweek is not null)
            .OrderBy(fixture => fixture.Gameweek)
            .ThenBy(fixture => fixture.Kickoff)
            .Take(Math.Clamp(gameweeks, 1, 5))
            .Select(fixture => new
            {
                fixture.Gameweek,
                fixture.GameweekName,
                fixture.Kickoff,
                fixture.IsHome,
                fixture.HomeTeamId,
                fixture.AwayTeamId,
                fixture.Difficulty
            })
            .ToArray();

        return Serialize(new
        {
            player.PlayerId,
            player.PlayerName,
            fixtures,
            source = "Official FPL element-summary data"
        });
    }

    public async Task<string> GetTransferOptionsAsync(
        FplCoachContext context,
        string playerName,
        int limit,
        CancellationToken cancellationToken)
    {
        var player = FindOwnedPlayer(context, playerName);
        if (player is null)
        {
            return NotFound(playerName);
        }

        var recommendations = await transferRecommendationService.GetRecommendationsAsync(
            context.TeamId,
            Math.Clamp(limit, 1, 10),
            cancellationToken);
        var options = recommendations.Recommendations
            .Where(item => item.PlayerOut.PlayerId == player.PlayerId)
            .Take(Math.Clamp(limit, 1, 10))
            .Select(item => new
            {
                PlayerOut = item.PlayerOut,
                PlayerIn = item.PlayerIn,
                item.PriceDifference,
                item.ExpectedPointGains,
                item.ConfidenceScore,
                item.Explanations
            })
            .ToArray();

        return Serialize(new
        {
            player.PlayerId,
            player.PlayerName,
            context.Bank,
            options,
            source = "Touchline transfer recommendation engine"
        });
    }

    private static FplCoachSquadPlayer? FindOwnedPlayer(FplCoachContext context, string playerName)
    {
        var search = playerName.Trim();
        if (search.Length == 0)
        {
            return null;
        }

        var exact = context.Squad.FirstOrDefault(player =>
            player.PlayerName.Equals(search, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var partialMatches = context.Squad
            .Where(player =>
                player.PlayerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                search.Contains(player.PlayerName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        return partialMatches.Length == 1 ? partialMatches[0] : null;
    }

    private static string NotFound(string playerName) => Serialize(new
    {
        error = $"Player '{playerName}' was not found in the connected 15-player squad. Do not infer facts for this player."
    });

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SerializerOptions);
}