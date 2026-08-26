using Backend.Coach.Models;
using Backend.Recommendation.Transfer;
using Backend.Services;
using System.Text.RegularExpressions;

namespace Backend.Coach;

public sealed class FplCoachFactService(
    IFplDataService fplDataService,
    ITransferRecommendationService transferRecommendationService) : IFplCoachFactService
{
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

    public async Task<PlayerReplacementResult> GetTransferCandidatesAsync(
        FplCoachContext context,
        int playerId,
        int limit,
        CancellationToken cancellationToken)
    {
        var player = context.Squad.SingleOrDefault(item => item.PlayerId == playerId)
            ?? throw new KeyNotFoundException($"Player {playerId} was not found in the connected 15-player squad.");
        var requestedLimit = Math.Clamp(limit, 1, 5);

        var recommendations = await transferRecommendationService.GetReplacementRecommendationsAsync(
            context.TeamId,
            player.PlayerId,
            requestedLimit,
            cancellationToken);
        var playerRecommendations = recommendations.Recommendations
            .Where(item => item.PlayerOut.PlayerId == player.PlayerId)
            .ToArray();
        var candidates = playerRecommendations
            .Take(requestedLimit)
            .Select((item, index) => MapCandidate(item, index + 1))
            .ToArray();
        var salePrice = recommendations.SelectedPlayerSellingPrice
            ?? playerRecommendations.FirstOrDefault()?.PlayerOut.Price
            ?? player.Price;
        var maximumPurchasePrice = salePrice + recommendations.Bank;

        return new PlayerReplacementResult(
            new CoachTransferPlayer(player.PlayerId, player.PlayerName, player.TeamName, player.Position, salePrice),
            recommendations.Bank,
            maximumPurchasePrice,
            5,
            candidates,
            "Touchline transfer recommendation engine; FPL budget, position, ownership, availability, expected-minutes, and three-player club rules enforced in C#");
    }

    private static CoachReplacementCandidate MapCandidate(
        Backend.Recommendation.Transfer.Models.TransferRecommendation recommendation,
        int rank)
    {
        var gain = recommendation.ExpectedPointGains.Single(item => item.Gameweeks == 5);
        var reason = recommendation.Explanations
            .Where(item => item.Factor is "Expected points" or "Fixture quality" or "Budget")
            .Select(item => item.Explanation)
            .FirstOrDefault() ?? "Higher deterministic projected points over the next five gameweeks.";
        return new CoachReplacementCandidate(
            rank,
            new CoachTransferPlayer(
                recommendation.PlayerIn.PlayerId,
                recommendation.PlayerIn.PlayerName,
                recommendation.PlayerIn.TeamName,
                recommendation.PlayerIn.Position,
                recommendation.PlayerIn.Price),
            recommendation.PriceDifference,
            gain.PlayerOutPoints,
            gain.PlayerInPoints,
            gain.ExpectedPointGain,
            recommendation.ConfidenceScore,
            reason);
    }

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