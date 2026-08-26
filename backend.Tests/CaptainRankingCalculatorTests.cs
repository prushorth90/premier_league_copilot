using Backend.Models;
using Backend.Recommendation.Captain;
using Backend.Recommendation.Captain.Factors;
using Backend.Recommendation.Captain.Models;
using Backend.Recommendation.Models;

namespace Backend.Tests;

public class CaptainRankingCalculatorTests
{
    [Fact]
    public void RankUsesAllFactorsAndReturnsBestOrder()
    {
        var calculator = CreateCalculator();
        var candidates = new[]
        {
            CreateContext(1, "Best", 8m, 90m, 0.5m, 0.7m, 0.3m),
            CreateContext(2, "Safe", 7.5m, 90m, 0.8m, 0.3m, 0.2m),
            CreateContext(3, "Doubt", 9m, 60m, 1m, 0.8m, 0.2m, "d", 25)
        };

        var result = calculator.Rank(candidates);

        Assert.Equal(["Best", "Doubt", "Safe"], result.Select(item => item.PlayerName));
        Assert.Equal(10.90m, result[0].RankingScore);
        Assert.Equal(
            ["Projected points", "Expected minutes", "Fixture quality", "Attacking potential", "Availability"],
            result[0].Factors.Select(item => item.Factor));
        Assert.Equal(result[0].RankingScore, result[0].Factors.Sum(item => item.Score));
    }

    [Fact]
    public void RankDemotesUnavailablePlayerDespiteHigherProjection()
    {
        var result = CreateCalculator().Rank(
        [
            CreateContext(1, "Available", 6m, 90m, 0m, 0.2m, 0.2m),
            CreateContext(2, "Injured", 10m, 90m, 1m, 1m, 0.5m, "i", 0)
        ]);

        Assert.Equal("Available", result[0].PlayerName);
        Assert.Equal(-10m, result[1].Factors.Single(item => item.Factor == "Availability").Score);
    }

    [Fact]
    public void RankBreaksExactTiesByPlayerId()
    {
        var result = CreateCalculator().Rank(
        [
            CreateContext(20, "Later", 6m, 90m, 0m, 0.2m, 0.2m),
            CreateContext(10, "Earlier", 6m, 90m, 0m, 0.2m, 0.2m)
        ]);

        Assert.Equal([10, 20], result.Select(item => item.PlayerId));
    }

    private static CaptainRankingCalculator CreateCalculator() => new(
    [
        new ProjectedPointsCaptainFactor(),
        new ExpectedMinutesCaptainFactor(),
        new FixtureQualityCaptainFactor(),
        new AttackingPotentialCaptainFactor(),
        new AvailabilityCaptainFactor()
    ]);

    private static CaptainCandidateContext CreateContext(
        int id,
        string name,
        decimal projectedPoints,
        decimal expectedMinutes,
        decimal fixtureQuality,
        decimal expectedGoalsPer90,
        decimal expectedAssistsPer90,
        string status = "a",
        int? chanceOfPlaying = null)
    {
        var player = new Player(
            id, id, name, "Player", name, 1, 3, 75, 0, 0, 0, 0,
            expectedGoalsPer90, expectedAssistsPer90, status, "", chanceOfPlaying);
        var projection = new PlayerProjection(
            id,
            name,
            DateTimeOffset.UtcNow,
            expectedMinutes,
            [new ProjectionHorizon(
                1,
                projectedPoints,
                [
                    new ProjectionFactorBreakdown("Fixture difficulty", fixtureQuality, "Fixture"),
                    new ProjectionFactorBreakdown("Venue", 0m, "Venue")
                ],
                [])]);

        return new(player, "Test FC", "MID", projection);
    }
}