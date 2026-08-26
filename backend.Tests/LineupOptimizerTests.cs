using Backend.Models;
using Backend.Recommendation.Lineup;
using Backend.Recommendation.Lineup.Models;
using Backend.Recommendation.Models;

namespace Backend.Tests;

public class LineupOptimizerTests
{
    private readonly FplFormationValidator validator = new();

    [Theory]
    [InlineData(3, 4, 3)]
    [InlineData(3, 5, 2)]
    [InlineData(4, 3, 3)]
    [InlineData(4, 4, 2)]
    [InlineData(4, 5, 1)]
    [InlineData(5, 2, 3)]
    [InlineData(5, 3, 2)]
    [InlineData(5, 4, 1)]
    public void IsValidAcceptsEveryLegalFormation(int defenders, int midfielders, int forwards)
    {
        var positions = new[] { "GKP" }
            .Concat(Enumerable.Repeat("DEF", defenders))
            .Concat(Enumerable.Repeat("MID", midfielders))
            .Concat(Enumerable.Repeat("FWD", forwards));

        Assert.True(validator.IsValid(positions));
    }

    [Theory]
    [InlineData(2, 5, 3)]
    [InlineData(6, 3, 1)]
    [InlineData(5, 5, 0)]
    [InlineData(3, 3, 4)]
    public void IsValidRejectsIllegalPositionCounts(int defenders, int midfielders, int forwards)
    {
        var positions = new[] { "GKP" }
            .Concat(Enumerable.Repeat("DEF", defenders))
            .Concat(Enumerable.Repeat("MID", midfielders))
            .Concat(Enumerable.Repeat("FWD", forwards));

        Assert.False(validator.IsValid(positions));
    }

    [Fact]
    public void OptimizeChoosesHighestRankedLegalFormationAndOrdersBench()
    {
        var candidates = CreateValidSquad();
        var optimizer = new LineupOptimizer(validator);

        var result = optimizer.Optimize(candidates);

        Assert.Equal("3-5-2", result.Formation);
        Assert.Equal(11, result.StartingXi.Count);
        Assert.True(validator.IsValid(result.StartingXi.Select(player => player.Position)));
        Assert.Equal(4, result.Bench.Count);
        Assert.Equal("GKP", result.Bench[^1].Position);
        Assert.Equal([1], result.StartingXi[0].Projections.Select(projection => projection.Gameweeks));
        Assert.Equal([12, 13, 14, 15], result.Bench.Select(player => player.RecommendedSquadPosition));
        Assert.Equal(result.Bench.Take(3).OrderByDescending(player => player.RankingScore), result.Bench.Take(3));
        Assert.Contains(result.Changes, change => change.PlayerId == 12 && change.ChangeType == "Moved to starting XI");
        Assert.Contains(result.Changes, change => change.PlayerId == 15 && change.ChangeType == "Moved to bench");
    }

    [Fact]
    public void OptimizeUsesExpectedMinutesWhenProjectedPointsAreEqual()
    {
        var candidates = CreateValidSquad().ToList();
        candidates[4] = CreateCandidate(5, "DEF", 1m, 10m, 4);
        candidates[5] = CreateCandidate(6, "DEF", 1m, 90m, 12);
        var optimizer = new LineupOptimizer(validator);

        var result = optimizer.Optimize(candidates);

        Assert.Contains(result.StartingXi, player => player.PlayerId == 6);
        Assert.Contains(result.Bench, player => player.PlayerId == 5);
    }

    [Fact]
    public void OptimizeRejectsIncompleteSquad()
    {
        var optimizer = new LineupOptimizer(validator);

        var exception = Assert.Throws<InvalidOperationException>(() => optimizer.Optimize(CreateValidSquad().Take(14)));

        Assert.Contains("15 distinct players", exception.Message);
    }

    [Fact]
    public void OptimizeRejectsInvalidSquadPositionAllocation()
    {
        var candidates = CreateValidSquad().ToList();
        candidates[^1] = CreateCandidate(99, "MID", 5m, 90m, 15);
        var optimizer = new LineupOptimizer(validator);

        var exception = Assert.Throws<InvalidOperationException>(() => optimizer.Optimize(candidates));

        Assert.Contains("position allocation", exception.Message);
    }

    private static IReadOnlyList<LineupCandidateContext> CreateValidSquad() =>
    [
        CreateCandidate(1, "GKP", 7m, 90m, 1),
        CreateCandidate(2, "GKP", 2m, 90m, 15),
        CreateCandidate(3, "DEF", 8m, 90m, 2),
        CreateCandidate(4, "DEF", 7m, 90m, 3),
        CreateCandidate(5, "DEF", 1m, 90m, 4),
        CreateCandidate(6, "DEF", 0.5m, 90m, 12),
        CreateCandidate(7, "DEF", 0.4m, 90m, 13),
        CreateCandidate(8, "MID", 10m, 90m, 5),
        CreateCandidate(9, "MID", 9m, 90m, 6),
        CreateCandidate(10, "MID", 8m, 90m, 7),
        CreateCandidate(11, "MID", 7m, 90m, 8),
        CreateCandidate(12, "MID", 6m, 90m, 14),
        CreateCandidate(13, "FWD", 10m, 90m, 9),
        CreateCandidate(14, "FWD", 9m, 90m, 10),
        CreateCandidate(15, "FWD", 1m, 90m, 11)
    ];

    private static LineupCandidateContext CreateCandidate(int id, string position, decimal points, decimal minutes, int currentPosition)
    {
        var positionId = position switch { "GKP" => 1, "DEF" => 2, "MID" => 3, _ => 4 };
        var player = new Player(id, id, $"Player {id}", "", $"Player {id}", 1, positionId, 50, 0, 0, 0, 0, 0, 0, "a", "", null);
        var projection = new PlayerProjection(
            id,
            player.DisplayName,
            DateTimeOffset.UtcNow,
            minutes,
            [new ProjectionHorizon(1, points, [], [])]);
        return new(player, "Test FC", position, currentPosition, projection);
    }
}