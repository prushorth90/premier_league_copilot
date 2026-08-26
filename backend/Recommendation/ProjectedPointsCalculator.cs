using Backend.Models;
using Backend.Recommendation.Factors;
using Backend.Recommendation.Models;

namespace Backend.Recommendation;

public sealed class ProjectedPointsCalculator(
    IEnumerable<IProjectionFactor> factors,
    TimeProvider timeProvider) : IProjectedPointsCalculator
{
    private static readonly int[] Horizons = [1, 3, 5];
    private readonly IReadOnlyList<IProjectionFactor> orderedFactors = factors.OrderBy(factor => factor.Order).ToArray();

    public PlayerProjection Calculate(Player player, PlayerHistory history)
    {
        var context = new ProjectionContext(
            player,
            history,
            CalculateExpectedMinutes(history),
            CalculateHistoricalPointsPer90(history));
        var fixtureProjections = history.Fixtures
            .Where(fixture => fixture.Gameweek is not null)
            .OrderBy(fixture => fixture.Gameweek)
            .ThenBy(fixture => fixture.Kickoff)
            .Select(fixture => CalculateFixture(context, fixture))
            .ToArray();
        var gameweeks = fixtureProjections
            .Select(projection => projection.Gameweek!.Value)
            .Distinct()
            .Order()
            .ToArray();

        var horizonProjections = Horizons
            .Select(horizon => CalculateHorizon(horizon, gameweeks, fixtureProjections))
            .ToArray();

        return new(player.Id, player.DisplayName, timeProvider.GetUtcNow(), context.ExpectedMinutes, horizonProjections);
    }

    private FixtureProjection CalculateFixture(ProjectionContext context, PlayerFixture fixture)
    {
        var score = 0m;
        var breakdown = new List<ProjectionFactorBreakdown>(orderedFactors.Count);

        foreach (var factor in orderedFactors)
        {
            var contribution = factor.Calculate(context, fixture, score);
            breakdown.Add(contribution);
            score += contribution.Contribution;
        }

        var projectedPoints = Round(Math.Max(0m, score));
        var roundedBreakdown = breakdown
            .Select(item => item with { Contribution = Round(item.Contribution) })
            .ToList();
        var adjustment = projectedPoints - roundedBreakdown.Sum(item => item.Contribution);
        if (adjustment != 0m)
        {
            roundedBreakdown.Add(new ProjectionFactorBreakdown(
                "Floor and rounding",
                adjustment,
                "Reconciles rounded factors and prevents negative projected points."));
        }

        return new(
            fixture.Id,
            fixture.Gameweek,
            projectedPoints,
            roundedBreakdown);
    }

    private static ProjectionHorizon CalculateHorizon(
        int horizon,
        IReadOnlyList<int> gameweeks,
        IReadOnlyList<FixtureProjection> fixtures)
    {
        var selectedGameweeks = gameweeks.Take(horizon).ToHashSet();
        var selectedFixtures = fixtures.Where(fixture => fixture.Gameweek is int gameweek && selectedGameweeks.Contains(gameweek)).ToArray();
        var factors = selectedFixtures
            .SelectMany(fixture => fixture.Factors)
            .GroupBy(factor => factor.Factor)
            .Select(group => new ProjectionFactorBreakdown(
                group.Key,
                Round(group.Sum(item => item.Contribution)),
                string.Join(" ", group.Select(item => item.Explanation).Distinct())))
            .ToArray();

        return new(horizon, Round(selectedFixtures.Sum(fixture => fixture.ProjectedPoints)), factors, selectedFixtures);
    }

    private static decimal CalculateExpectedMinutes(PlayerHistory history)
    {
        var recentAppearances = history.CurrentSeason
            .OrderByDescending(item => item.Gameweek)
            .Take(5)
            .ToArray();

        return recentAppearances.Length == 0
            ? 60m
            : Math.Clamp(recentAppearances.Average(item => (decimal)item.Minutes), 0m, 90m);
    }

    private static decimal CalculateHistoricalPointsPer90(PlayerHistory history)
    {
        var seasons = history.PreviousSeasons
            .Where(season => season.Minutes > 0)
            .TakeLast(3)
            .ToArray();

        return seasons.Length == 0
            ? 0m
            : seasons.Sum(season => season.Points) * 90m / seasons.Sum(season => season.Minutes);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}