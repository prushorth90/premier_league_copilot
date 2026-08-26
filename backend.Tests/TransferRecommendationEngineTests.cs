using Backend.Models;
using Backend.Recommendation.Models;
using Backend.Recommendation.Transfer;
using Backend.Recommendation.Transfer.Models;

namespace Backend.Tests;

public class TransferRecommendationEngineTests
{
    private readonly TransferRecommendationEngine engine = new();

    [Fact]
    public void RankCalculatesHorizonGainsConfidencePriceAndExplanations()
    {
        var squad = CreateValidSquad();
        var incoming = CreateContext(101, 6, 2, 55, 6m, 18m, 30m, expectedMinutes: 90m, fixtureQuality: 1m);

        var result = engine.Rank(squad, [incoming], bank: 5);

        var recommendation = Assert.Single(result, item => item.PlayerOut.PlayerId == 4);
        Assert.Equal(101, recommendation.PlayerIn.PlayerId);
        Assert.Equal(0.5m, recommendation.PriceDifference);
        Assert.Equal(2m, recommendation.WeightedGain);
        Assert.Equal(100m, recommendation.ConfidenceScore);
        Assert.Equal([1, 3, 5], recommendation.ExpectedPointGains.Select(gain => gain.Gameweeks));
        Assert.All(recommendation.ExpectedPointGains, gain => Assert.Equal(gain.Gameweeks * 2m, gain.ExpectedPointGain));
        Assert.Equal(
            ["Expected points", "Fixture quality", "Expected minutes", "Availability", "Budget"],
            recommendation.Explanations.Select(explanation => explanation.Factor));
        Assert.Equal(3m, recommendation.Explanations.Single(explanation => explanation.Factor == "Fixture quality").Score);
    }

    [Fact]
    public void RankUsesSellingPriceRatherThanCurrentMarketPriceForBudget()
    {
        var squad = CreateValidSquad();
        var incoming = CreateContext(101, 6, 2, 56, 10m, 30m, 50m);

        var result = engine.Rank(squad, [incoming], bank: 5);

        Assert.Empty(result);
    }

    [Fact]
    public void RankPairsOnlyMatchingPositionsAndFiltersOwnedUnavailableAndNonImprovingPlayers()
    {
        var squad = CreateValidSquad();
        var market = new[]
        {
            CreateContext(3, 6, 2, 50, 10m, 30m, 50m),
            CreateContext(101, 6, 3, 50, 10m, 30m, 50m),
            CreateContext(102, 6, 2, 50, 10m, 30m, 50m, status: "i"),
            CreateContext(103, 6, 2, 50, 1m, 3m, 5m),
            CreateContext(104, 6, 2, 50, 10m, 30m, 50m, status: "n"),
            CreateContext(105, 6, 2, 50, 10m, 30m, 50m, expectedMinutes: 0m)
        };

        var result = engine.Rank(squad, market, bank: 0);

        Assert.NotEmpty(result);
        Assert.All(result, recommendation => Assert.Equal(recommendation.PlayerOut.Position, recommendation.PlayerIn.Position));
        Assert.DoesNotContain(result, recommendation => recommendation.PlayerIn.PlayerId is 3 or 102 or 103 or 104 or 105);
    }

    [Fact]
    public void RankEnforcesThreePlayerClubLimitButAllowsSameClubReplacement()
    {
        var squad = CreateValidSquad();
        var incoming = CreateContext(101, 1, 2, 50, 10m, 30m, 50m);

        var result = engine.Rank(squad, [incoming], bank: 0);

        var recommendation = Assert.Single(result);
        Assert.Equal(6, recommendation.PlayerOut.PlayerId);
        Assert.Equal(1, recommendation.PlayerIn.PlayerId == 101 ? incoming.Player.TeamId : 0);
    }

    [Fact]
    public void RankAllowsDoubtfulPlayerAndReducesConfidence()
    {
        var squad = CreateValidSquad();
        var available = CreateContext(101, 6, 2, 50, 10m, 30m, 50m);
        var doubtful = CreateContext(102, 6, 2, 50, 10m, 30m, 50m, status: "d", chanceOfPlaying: 50);

        var result = engine.Rank(squad, [available, doubtful], bank: 0);

        var availableRecommendation = result.First(item => item.PlayerIn.PlayerId == 101);
        var doubtfulRecommendation = result.First(item => item.PlayerIn.PlayerId == 102);
        Assert.True(availableRecommendation.ConfidenceScore > doubtfulRecommendation.ConfidenceScore);
        Assert.Contains("50%", doubtfulRecommendation.Explanations.Single(item => item.Factor == "Availability").Explanation);
    }

    [Fact]
    public void RankHonorsLimitAndUsesDeterministicPlayerIdTieBreak()
    {
        var squad = CreateValidSquad();
        var market = new[]
        {
            CreateContext(102, 6, 2, 50, 10m, 30m, 50m),
            CreateContext(101, 7, 2, 50, 10m, 30m, 50m)
        };

        var result = engine.Rank(squad, market, bank: 0, limit: 1);

        Assert.Single(result);
        Assert.Equal(101, result[0].PlayerIn.PlayerId);
    }

    [Fact]
    public void RankCombinationsAllowsExpensiveTransferFundedBySecondSale()
    {
        var squad = CreateValidSquad();
        var expensiveDefender = CreateContext(201, 6, 2, 70, 10m, 30m, 50m);
        var budgetMidfielder = CreateContext(202, 7, 3, 40, 5m, 15m, 25m);

        var singles = engine.Rank(squad, [expensiveDefender, budgetMidfielder], bank: 10);
        var combinations = engine.RankCombinations(squad, [expensiveDefender, budgetMidfielder], bank: 10);

        Assert.DoesNotContain(singles, recommendation => recommendation.PlayerIn.PlayerId == 201);
        var combination = combinations.First(recommendation =>
            recommendation.Transfers.Select(transfer => transfer.PlayerIn.PlayerId).Order().SequenceEqual([201, 202]));
        Assert.Equal(1m, combination.TotalPriceDifference);
        Assert.Equal([3, 5], combination.ExpectedPointGains.Select(gain => gain.Gameweeks));
        Assert.Equal(21m, combination.ExpectedPointGains.Single(gain => gain.Gameweeks == 3).ExpectedPointGain);
        Assert.Equal(35m, combination.ExpectedPointGains.Single(gain => gain.Gameweeks == 5).ExpectedPointGain);
        Assert.Equal(7m, combination.WeightedGain);
    }

    [Fact]
    public void RankCombinationsRejectsPairOnePriceUnitOverCombinedBudget()
    {
        var market = new[]
        {
            CreateContext(201, 6, 2, 71, 10m, 30m, 50m),
            CreateContext(202, 7, 3, 40, 5m, 15m, 25m)
        };

        var result = engine.RankCombinations(CreateValidSquad(), market, bank: 10);

        Assert.Empty(result);
    }

    [Fact]
    public void RankCombinationsRejectsResultingClubLimitViolation()
    {
        var market = new[]
        {
            CreateContext(201, 1, 4, 50, 10m, 30m, 50m),
            CreateContext(202, 1, 4, 50, 9m, 27m, 45m)
        };

        var result = engine.RankCombinations(CreateValidSquad(), market, bank: 0);

        Assert.Empty(result);
    }

    [Fact]
    public void RankCombinationsReturnsDistinctPlayersAndPreservesPositions()
    {
        var market = new[]
        {
            CreateContext(201, 6, 2, 50, 10m, 30m, 50m),
            CreateContext(202, 7, 2, 50, 9m, 27m, 45m),
            CreateContext(203, 8, 3, 50, 8m, 24m, 40m)
        };

        var result = engine.RankCombinations(CreateValidSquad(), market, bank: 0, limit: 5);

        Assert.Equal(5, result.Count);
        Assert.All(result, combination =>
        {
            Assert.Equal(2, combination.Transfers.Select(transfer => transfer.PlayerOut.PlayerId).Distinct().Count());
            Assert.Equal(2, combination.Transfers.Select(transfer => transfer.PlayerIn.PlayerId).Distinct().Count());
            Assert.All(combination.Transfers, transfer => Assert.Equal(transfer.PlayerOut.Position, transfer.PlayerIn.Position));
        });
        Assert.Equal(result.OrderByDescending(combination => combination.WeightedGain), result);
    }

    [Fact]
    public void RankCombinationsFiltersNonImprovingPairs()
    {
        var market = new[]
        {
            CreateContext(201, 6, 2, 50, 4m, 9m, 15m),
            CreateContext(202, 7, 3, 50, 4m, 9m, 15m)
        };

        var result = engine.RankCombinations(CreateValidSquad(), market, bank: 0);

        Assert.Empty(result);
    }

    [Fact]
    public void RankRejectsIncompleteSquad()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => engine.Rank(CreateValidSquad().Take(14).ToArray(), [], 0));

        Assert.Contains("15 distinct players", exception.Message);
    }

    [Fact]
    public void RankRejectsInvalidPositionAllocation()
    {
        var squad = CreateValidSquad().ToArray();
        squad[^1] = CreateContext(15, 5, 3, 50, 4m, 12m, 20m, sellingPrice: 50);

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Rank(squad, [], 0));

        Assert.Contains("position allocation", exception.Message);
    }

    [Fact]
    public void RankRejectsSquadAlreadyOverClubLimit()
    {
        var squad = CreateValidSquad().ToArray();
        squad[^1] = CreateContext(15, 1, 4, 50, 4m, 12m, 20m, sellingPrice: 50);

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Rank(squad, [], 0));

        Assert.Contains("club limit", exception.Message);
    }

    [Fact]
    public void RankRejectsNegativeBankAndNonPositiveLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Rank(CreateValidSquad(), [], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Rank(CreateValidSquad(), [], 0, 0));
    }

    private static IReadOnlyList<TransferPlayerContext> CreateValidSquad() =>
    [
        CreateContext(1, 1, 1, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(2, 2, 1, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(3, 3, 2, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(4, 4, 2, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(5, 5, 2, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(6, 1, 2, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(7, 2, 2, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(8, 3, 3, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(9, 4, 3, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(10, 5, 3, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(11, 1, 3, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(12, 2, 3, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(13, 3, 4, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(14, 4, 4, 60, 4m, 12m, 20m, sellingPrice: 50),
        CreateContext(15, 5, 4, 60, 4m, 12m, 20m, sellingPrice: 50)
    ];

    private static TransferPlayerContext CreateContext(
        int id,
        int teamId,
        int positionId,
        int price,
        decimal oneGameweek,
        decimal threeGameweeks,
        decimal fiveGameweeks,
        decimal expectedMinutes = 90m,
        decimal fixtureQuality = 0m,
        string status = "a",
        int? chanceOfPlaying = null,
        int? sellingPrice = null)
    {
        var position = positionId switch { 1 => "GKP", 2 => "DEF", 3 => "MID", _ => "FWD" };
        var player = new Player(id, id, $"Player {id}", "", $"Player {id}", teamId, positionId, price, 0, 0, 0, 0, 0, 0, status, "", chanceOfPlaying);
        var projection = new PlayerProjection(
            id,
            player.DisplayName,
            DateTimeOffset.UtcNow,
            expectedMinutes,
            [
                CreateHorizon(1, oneGameweek, fixtureQuality),
                CreateHorizon(3, threeGameweeks, fixtureQuality),
                CreateHorizon(5, fiveGameweeks, fixtureQuality)
            ]);
        return new(player, $"Team {teamId}", position, projection, sellingPrice);
    }

    private static ProjectionHorizon CreateHorizon(int gameweeks, decimal points, decimal fixtureQuality) => new(
        gameweeks,
        points,
        [new ProjectionFactorBreakdown("Fixture difficulty", fixtureQuality, "Fixture quality")],
        [new FixtureProjection(gameweeks, gameweeks, points, [])]);
}
