using Backend.Recommendation.Models;
using Backend.Recommendation.Transfer.Models;

namespace Backend.Recommendation.Transfer;

public sealed class TransferRecommendationEngine : ITransferRecommendationEngine
{
    private static readonly int[] Horizons = [1, 3, 5];
    private const decimal MinimumExpectedMinutes = 30m;

    public IReadOnlyList<TransferRecommendation> Rank(
        IReadOnlyList<TransferPlayerContext> squad,
        IReadOnlyList<TransferPlayerContext> market,
        int bank,
        int limit = 20)
    {
        ValidateInputs(squad, bank, limit);
        var ownedIds = squad.Select(context => context.Player.Id).ToHashSet();
        var clubCounts = squad.GroupBy(context => context.Player.TeamId).ToDictionary(group => group.Key, group => group.Count());

        return squad
            .SelectMany(playerOut => market
                .Where(playerIn => IsValidReplacement(playerOut, playerIn, ownedIds, clubCounts, bank))
                .Select(playerIn => CreateRecommendation(playerOut, playerIn)))
            .Where(recommendation => recommendation.WeightedGain > 0m)
            .OrderByDescending(recommendation => recommendation.WeightedGain)
            .ThenByDescending(recommendation => recommendation.ConfidenceScore)
            .ThenByDescending(recommendation => recommendation.ExpectedPointGains.Single(gain => gain.Gameweeks == 5).ExpectedPointGain)
            .ThenBy(recommendation => recommendation.PlayerIn.PlayerId)
            .ThenBy(recommendation => recommendation.PlayerOut.PlayerId)
            .Take(limit)
            .ToArray();
    }

    private static bool IsValidReplacement(
        TransferPlayerContext playerOut,
        TransferPlayerContext playerIn,
        IReadOnlySet<int> ownedIds,
        IReadOnlyDictionary<int, int> clubCounts,
        int bank)
    {
        if (ownedIds.Contains(playerIn.Player.Id)
            || playerIn.Player.PositionId != playerOut.Player.PositionId
            || playerIn.Player.Status is not ("a" or "d")
            || playerIn.Projection.ExpectedMinutes < MinimumExpectedMinutes
            || playerIn.Player.Price > (playerOut.SellingPrice ?? playerOut.Player.Price) + bank)
        {
            return false;
        }

        var incomingClubCount = clubCounts.GetValueOrDefault(playerIn.Player.TeamId)
            - (playerOut.Player.TeamId == playerIn.Player.TeamId ? 1 : 0)
            + 1;
        return incomingClubCount <= 3;
    }

    private static TransferRecommendation CreateRecommendation(
        TransferPlayerContext playerOut,
        TransferPlayerContext playerIn)
    {
        var gains = Horizons.Select(gameweeks =>
        {
            var outPoints = Horizon(playerOut, gameweeks).ProjectedPoints;
            var inPoints = Horizon(playerIn, gameweeks).ProjectedPoints;
            return new TransferHorizonGain(gameweeks, outPoints, inPoints, Round(inPoints - outPoints));
        }).ToArray();
        var weightedGain = Round(
            gains.Single(gain => gain.Gameweeks == 1).ExpectedPointGain * 0.5m
            + gains.Single(gain => gain.Gameweeks == 3).ExpectedPointGain / 3m * 0.3m
            + gains.Single(gain => gain.Gameweeks == 5).ExpectedPointGain / 5m * 0.2m);
        var fixtureDelta = Round(FixtureQuality(playerIn) - FixtureQuality(playerOut));
        var positiveHorizons = gains.Count(gain => gain.ExpectedPointGain > 0m);
        var fixtureCoverage = Horizons.Count(gameweeks => Horizon(playerIn, gameweeks).Fixtures.Count > 0);
        var availabilityScore = playerIn.Player.Status == "a" ? 1m : (playerIn.Player.ChanceOfPlayingNextRound ?? 50) / 100m;
        var baseConfidence = Math.Clamp(
            playerIn.Projection.ExpectedMinutes / 90m * 45m
            + availabilityScore * 25m
            + positiveHorizons / 3m * 20m
            + fixtureCoverage / 3m * 10m,
            0m,
            100m);
        var playingTimeEvidence = Math.Clamp(playerIn.Projection.ExpectedMinutes / 60m, 0m, 1m);
        var confidence = Round(baseConfidence * playingTimeEvidence);
        var sellingPrice = playerOut.SellingPrice ?? playerOut.Player.Price;

        return new(
            MapPlayer(playerOut, sellingPrice),
            MapPlayer(playerIn, playerIn.Player.Price),
            Round((playerIn.Player.Price - sellingPrice) / 10m),
            gains,
            weightedGain,
            confidence,
            [
                new("Expected points", weightedGain, $"Weighted improvement across 1, 3, and 5 gameweeks: {weightedGain:+0.00;-0.00;0.00} points per gameweek."),
                new("Fixture quality", fixtureDelta, $"Fixture and venue contribution changes by {fixtureDelta:+0.00;-0.00;0.00} across the projection horizons."),
                new("Expected minutes", Round(playerIn.Projection.ExpectedMinutes - playerOut.Projection.ExpectedMinutes), $"Expected minutes change from {playerOut.Projection.ExpectedMinutes:0} to {playerIn.Projection.ExpectedMinutes:0}."),
                new("Availability", availabilityScore, playerIn.Player.Status == "a" ? "Incoming player is currently available." : $"Incoming player is doubtful with {playerIn.Player.ChanceOfPlayingNextRound ?? 50}% chance of playing."),
                new("Budget", Round((sellingPrice - playerIn.Player.Price) / 10m), $"Uses £{playerIn.Player.Price / 10m:0.0}m of £{sellingPrice / 10m:0.0}m sale value plus bank.")
            ]);
    }

    private static TransferPlayer MapPlayer(TransferPlayerContext context, int price) => new(
        context.Player.Id,
        context.Player.DisplayName,
        context.TeamName,
        context.Position,
        price / 10m,
        context.Player.Status,
        Round(context.Projection.ExpectedMinutes));

    private static ProjectionHorizon Horizon(TransferPlayerContext context, int gameweeks) =>
        context.Projection.Horizons.Single(horizon => horizon.Gameweeks == gameweeks);

    private static decimal FixtureQuality(TransferPlayerContext context) => context.Projection.Horizons
        .SelectMany(horizon => horizon.Factors)
        .Where(factor => factor.Factor is "Fixture difficulty" or "Venue")
        .Sum(factor => factor.Contribution);

    private static void ValidateInputs(IReadOnlyList<TransferPlayerContext> squad, int bank, int limit)
    {
        if (squad.Count != 15 || squad.Select(context => context.Player.Id).Distinct().Count() != 15)
        {
            throw new InvalidOperationException("A valid FPL squad must contain 15 distinct players.");
        }

        if (squad.GroupBy(context => context.Player.TeamId).Any(group => group.Count() > 3))
        {
            throw new InvalidOperationException("The current squad exceeds the three-player club limit.");
        }

        var expectedPositions = new Dictionary<int, int> { [1] = 2, [2] = 5, [3] = 5, [4] = 3 };
        if (expectedPositions.Any(item => squad.Count(context => context.Player.PositionId == item.Key) != item.Value))
        {
            throw new InvalidOperationException("The squad does not have the required FPL position allocation.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(bank);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}