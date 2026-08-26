using Backend.Models;
using Backend.Recommendation;
using Backend.Recommendation.Factors;

namespace Backend.Tests;

public class ProjectedPointsCalculatorTests
{
    private static readonly DateTimeOffset CalculationTime = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
    public static TheoryData<string, int?, decimal> AvailabilityCases => new()
    {
        { "a", null, 7.25m },
        { "d", 50, 3.63m },
        { "i", 0, 0m },
        { "s", 0, 0m }
    };

    [Fact]
    public void CalculateReturnsExactOneThreeAndFiveGameweekProjections()
    {
        var calculator = CreateCalculator();
        var player = CreatePlayer();
        var history = CreateHistory(
            fixtures:
            [
                CreateFixture(1, 2, true, 2),
                CreateFixture(2, 3, false, 4),
                CreateFixture(3, 3, true, 3),
                CreateFixture(4, 4, false, 3),
                CreateFixture(5, 5, false, 5),
                CreateFixture(6, 6, true, 1)
            ]);

        var result = calculator.Calculate(player, history);

        Assert.Equal(CalculationTime, result.CalculatedAt);
        Assert.Equal(7.25m, result.Horizons.Single(item => item.Gameweeks == 1).ProjectedPoints);
        Assert.Equal(26.70m, result.Horizons.Single(item => item.Gameweeks == 3).ProjectedPoints);
        Assert.Equal(40.05m, result.Horizons.Single(item => item.Gameweeks == 5).ProjectedPoints);
        Assert.Equal(4, result.Horizons.Single(item => item.Gameweeks == 3).Fixtures.Count);
        Assert.Contains(
            result.Horizons[0].Factors,
            factor => factor.Factor == "Recent form" && factor.Contribution == 2.10m);
    }

    [Theory]
    [MemberData(nameof(AvailabilityCases))]
    public void CalculateAppliesAvailabilityLast(
        string status,
        int? chanceOfPlaying,
        decimal expectedPoints)
    {
        var player = CreatePlayer(status, chanceOfPlaying);
        var result = CreateCalculator().Calculate(
            player,
            CreateHistory(fixtures: [CreateFixture(1, 2, true, 2)]));

        var projection = result.Horizons[0];
        Assert.Equal(expectedPoints, projection.ProjectedPoints);
        Assert.Equal(
            projection.Fixtures[0].ProjectedPoints,
            Math.Max(0m, projection.Fixtures[0].Factors.Sum(item => item.Contribution)));
    }

    [Fact]
    public void CalculateUsesRecentMinutesAndThreeMostRecentHistoricalSeasons()
    {
        var history = CreateHistory(
            fixtures: [CreateFixture(1, 2, true, 3)],
            currentSeason:
            [
                CreateGameweekHistory(1, 90),
                CreateGameweekHistory(2, 45),
                CreateGameweekHistory(3, 0)
            ],
            previousSeasons:
            [
                CreateSeason("Old", 50, 900),
                CreateSeason("A", 100, 1800),
                CreateSeason("B", 120, 1800),
                CreateSeason("C", 140, 1800)
            ]);

        var fixture = CreateCalculator().Calculate(CreatePlayer(), history).Horizons[0].Fixtures[0];

        Assert.Contains(fixture.Factors, item =>
            item.Factor == "Expected playing time" && item.Contribution == 0.75m);
        Assert.Contains(fixture.Factors, item =>
            item.Factor == "Historical FPL points" && item.Contribution == 1.50m);
    }

    private static ProjectedPointsCalculator CreateCalculator() => new(
        [
            new PositionFactor(),
            new RecentFormFactor(),
            new ExpectedPlayingTimeFactor(),
            new FixtureDifficultyFactor(),
            new HomeAwayFactor(),
            new HistoricalPointsFactor(),
            new AvailabilityFactor()
        ],
        new FixedTimeProvider(CalculationTime));

    private static Player CreatePlayer(string status = "a", int? chanceOfPlaying = null) => new(
        10,
        100,
        "Test",
        "Player",
        "Test Player",
        1,
        3,
        75,
        100,
        5,
        6m,
        10m,
        0.4m,
        0.2m,
        status,
        string.Empty,
        chanceOfPlaying);

    private static PlayerHistory CreateHistory(
        IReadOnlyList<PlayerFixture> fixtures,
        IReadOnlyList<PlayerGameweekHistory>? currentSeason = null,
        IReadOnlyList<PlayerSeasonHistory>? previousSeasons = null) => new(
        fixtures,
        currentSeason ?? [CreateGameweekHistory(1, 90)],
        previousSeasons ?? [CreateSeason("2025/26", 180, 2700)]);

    private static PlayerFixture CreateFixture(int id, int gameweek, bool isHome, int difficulty) => new(
        id,
        gameweek,
        $"Gameweek {gameweek}",
        CalculationTime.AddDays(gameweek * 7),
        isHome,
        isHome ? 1 : 2,
        isHome ? 2 : 1,
        difficulty);

    private static PlayerGameweekHistory CreateGameweekHistory(int gameweek, int minutes) => new(
        10,
        gameweek,
        2,
        gameweek,
        true,
        CalculationTime.AddDays(-gameweek * 7),
        5,
        minutes,
        0,
        0,
        0,
        0,
        0,
        0,
        75,
        1000,
        0,
        0);

    private static PlayerSeasonHistory CreateSeason(string season, int points, int minutes) => new(
        season,
        100,
        50,
        60,
        points,
        minutes,
        0,
        0,
        0,
        0,
        0,
        0);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}