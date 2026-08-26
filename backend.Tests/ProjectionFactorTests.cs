using Backend.Models;
using Backend.Recommendation.Factors;
using Backend.Recommendation.Models;

namespace Backend.Tests;

public class ProjectionFactorTests
{
    [Theory]
    [InlineData(1, 1.8)]
    [InlineData(2, 1.7)]
    [InlineData(3, 1.5)]
    [InlineData(4, 1.4)]
    public void PositionFactorUsesPositionSpecificBaseline(int positionId, decimal expected)
    {
        var factor = new PositionFactor();

        Assert.Equal(expected, factor.Calculate(CreateContext(positionId: positionId), CreateFixture(), 0m).Contribution);
    }

    [Theory]
    [InlineData(1, 0.8)]
    [InlineData(3, 0)]
    [InlineData(5, -0.8)]
    public void FixtureDifficultyFactorRewardsEasierFixtures(int difficulty, decimal expected)
    {
        var factor = new FixtureDifficultyFactor();

        Assert.Equal(expected, factor.Calculate(CreateContext(), CreateFixture(difficulty: difficulty), 0m).Contribution);
    }

    [Theory]
    [InlineData(true, 0.25)]
    [InlineData(false, -0.1)]
    public void HomeAwayFactorUsesVenue(bool isHome, decimal expected)
    {
        var factor = new HomeAwayFactor();

        Assert.Equal(expected, factor.Calculate(CreateContext(), CreateFixture(isHome: isHome), 0m).Contribution);
    }

    [Fact]
    public void RecentFormFactorIsLinearAndExplainable()
    {
        var result = new RecentFormFactor().Calculate(CreateContext(form: 8m), CreateFixture(), 0m);

        Assert.Equal(2.8m, result.Contribution);
        Assert.Contains("8.0", result.Explanation);
    }

    private static ProjectionContext CreateContext(int positionId = 3, decimal form = 6m)
    {
        var player = new Player(1, 1, "Test", "Player", "Test", 1, positionId, 50, 0, 0, form, 0, 0.4m, 0.2m, "a", "", null);
        return new(player, new PlayerHistory([], [], []), 90m, 6m);
    }

    private static PlayerFixture CreateFixture(bool isHome = true, int difficulty = 3) => new(
        1,
        2,
        "Gameweek 2",
        DateTimeOffset.UtcNow,
        isHome,
        1,
        2,
        difficulty);
}