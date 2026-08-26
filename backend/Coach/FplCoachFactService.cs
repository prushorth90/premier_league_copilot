using System.Text.Json;
using Backend.Coach.Models;
using Backend.Recommendation.Transfer;
using Backend.Services;
using System.Text.RegularExpressions;

namespace Backend.Coach;

public sealed class FplCoachFactService(
    IFplDataService fplDataService,
    ITransferRecommendationService transferRecommendationService) : IFplCoachFactService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId)
    {
        var player = context.Squad.SingleOrDefault(item => item.PlayerId == playerId)
            ?? throw new KeyNotFoundException($"Player {playerId} was not found in the connected 15-player squad.");
        var (description, isAvailable, confidence) = DescribeStatus(player);
        return new PlayerAvailabilityResult(
            new CoachAvailabilityPlayer(player.PlayerId, player.PlayerName, player.TeamName, player.Position),
            player.Status,
            description,
            isAvailable,
            player.ChanceOfPlayingNextRound,
            ExtractExpectedReturn(player.News),
            confidence,
            string.IsNullOrWhiteSpace(player.News) ? "No current FPL news note." : player.News,
            "Official FPL bootstrap data");
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

    private static (string Description, bool IsAvailable, decimal Confidence) DescribeStatus(FplCoachSquadPlayer player) =>
        player.Status switch
        {
            "a" => ("Available", true, 95m),
            "d" => ("Doubtful", false, player.ChanceOfPlayingNextRound is null ? 70m : 85m),
            "i" => ("Injured", false, 95m),
            "s" => ("Suspended", false, 95m),
            "u" => ("Unavailable", false, 95m),
            "n" => ("Not in squad", false, 90m),
            _ => ("Unknown", false, 40m)
        };

    private static string? ExtractExpectedReturn(string news)
    {
        var match = Regex.Match(
            news,
            @"Expected back\s+([^.;]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}