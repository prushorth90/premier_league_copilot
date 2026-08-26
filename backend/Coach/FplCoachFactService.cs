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

    public async Task<PlayerFixtureWindowResult> GetUpcomingFixturesAsync(
        FplCoachContext context,
        int playerId,
        int gameweeks,
        CancellationToken cancellationToken)
    {
        var player = context.Squad.SingleOrDefault(item => item.PlayerId == playerId)
            ?? throw new KeyNotFoundException($"Player {playerId} was not found in the connected 15-player squad.");
        var requestedGameweeks = Math.Clamp(gameweeks, 1, 5);
        var historyTask = fplDataService.GetPlayerHistoryAsync(player.PlayerId, cancellationToken);
        var bootstrapTask = fplDataService.GetBootstrapDataAsync(cancellationToken);
        await Task.WhenAll(historyTask, bootstrapTask);
        var history = await historyTask;
        var bootstrap = await bootstrapTask;
        var teamNames = bootstrap.Teams.ToDictionary(team => team.Id, team => team.Name);
        var selectedGameweeks = history.Fixtures
            .Where(fixture => fixture.Gameweek is not null)
            .Select(fixture => fixture.Gameweek!.Value)
            .Distinct()
            .Order()
            .Take(requestedGameweeks)
            .ToHashSet();
        var fixtures = history.Fixtures
            .Where(fixture => fixture.Gameweek is int gameweek && selectedGameweeks.Contains(gameweek))
            .OrderBy(fixture => fixture.Gameweek)
            .ThenBy(fixture => fixture.Kickoff)
            .Select(fixture => new CoachUpcomingFixture(
                fixture.Id,
                fixture.Gameweek!.Value,
                fixture.GameweekName,
                fixture.Kickoff,
                teamNames.GetValueOrDefault(fixture.IsHome ? fixture.AwayTeamId : fixture.HomeTeamId) ?? "Unknown opponent",
                fixture.IsHome,
                fixture.IsHome ? "Home" : "Away",
                fixture.Difficulty))
            .ToArray();
        var summary = FixtureDifficultyCalculator.Calculate(fixtures.Select(fixture => fixture.Difficulty));
        var explanation = summary.ScheduleRating switch
        {
            "Favorable" => $"{player.PlayerName} has a favorable upcoming schedule based on an average FPL difficulty of {summary.AverageDifficulty:0.00}.",
            "Mixed" => $"{player.PlayerName} has a mixed upcoming schedule based on an average FPL difficulty of {summary.AverageDifficulty:0.00}.",
            "Difficult" => $"{player.PlayerName} has a difficult upcoming schedule based on an average FPL difficulty of {summary.AverageDifficulty:0.00}.",
            _ => $"No published upcoming fixtures were available for {player.PlayerName}."
        };

        return new PlayerFixtureWindowResult(
            new CoachFixturePlayer(player.PlayerId, player.PlayerName, player.TeamName, player.Position),
            requestedGameweeks,
            fixtures,
            summary.AverageDifficulty,
            summary.AggregateScore,
            summary.ScheduleRating,
            explanation,
            "Official FPL element-summary and bootstrap data");
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