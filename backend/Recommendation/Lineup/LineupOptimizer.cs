using Backend.Recommendation.Lineup.Models;
using Backend.Models;

namespace Backend.Recommendation.Lineup;

public sealed class LineupOptimizer(IFplFormationValidator formationValidator) : ILineupOptimizer
{
    private static readonly (int Defenders, int Midfielders, int Forwards)[] LegalFormations =
    [
        (3, 4, 3),
        (3, 5, 2),
        (4, 3, 3),
        (4, 4, 2),
        (4, 5, 1),
        (5, 2, 3),
        (5, 3, 2),
        (5, 4, 1)
    ];

    public (string Formation, IReadOnlyList<LineupPlayer> StartingXi, IReadOnlyList<LineupPlayer> Bench, IReadOnlyList<LineupChange> Changes) Optimize(
        IEnumerable<LineupCandidateContext> candidates)
    {
        var ranked = candidates.Select(CreatePlayer).ToArray();
        ValidateSquad(ranked);

        var best = LegalFormations
            .Select(formation => CreateFormation(ranked, formation))
            .Where(option => formationValidator.IsValid(option.Players.Select(player => player.Position)))
            .OrderByDescending(option => option.Score)
            .ThenBy(option => option.Name, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The squad cannot produce a valid FPL formation.");

        var startingIds = best.Players.Select(player => player.PlayerId).ToHashSet();
        var startingXi = OrderStartingXi(best.Players);
        var bench = OrderBench(ranked.Where(player => !startingIds.Contains(player.PlayerId)));
        var allRecommendations = startingXi.Concat(bench).ToDictionary(player => player.PlayerId);
        var changes = ranked
            .Where(player => (player.CurrentSquadPosition <= 11) != (allRecommendations[player.PlayerId].RecommendedSquadPosition <= 11))
            .Select(player => new LineupChange(
                player.PlayerId,
                player.PlayerName,
                allRecommendations[player.PlayerId].RecommendedSquadPosition <= 11 ? "Moved to starting XI" : "Moved to bench",
                player.CurrentSquadPosition,
                allRecommendations[player.PlayerId].RecommendedSquadPosition))
            .OrderBy(change => change.RecommendedSquadPosition)
            .ToArray();

        return (best.Name, startingXi, bench, changes);
    }

    private static LineupPlayer CreatePlayer(LineupCandidateContext context)
    {
        var projection = context.Projection.Horizons.Single(horizon => horizon.Gameweeks == 1).ProjectedPoints;
        var expectedMinutes = Math.Clamp(context.Projection.ExpectedMinutes, 0m, 90m);
        var rankingScore = projection * 0.8m + expectedMinutes / 90m * 2m;

        return new(
            context.Player.Id,
            context.Player.DisplayName,
            context.TeamName,
            context.Position,
            Round(projection),
            Round(expectedMinutes),
            Round(rankingScore),
            context.CurrentSquadPosition,
            0,
            context.Projection.Horizons
                .Select(horizon => new LineupHorizonProjection(horizon.Gameweeks, horizon.ProjectedPoints))
                .ToArray(),
            PlayerPhotoUrl.FromCode(context.Player.Code));
    }

    private static FormationOption CreateFormation(
        IReadOnlyList<LineupPlayer> ranked,
        (int Defenders, int Midfielders, int Forwards) formation)
    {
        var players = Take(ranked, "GKP", 1)
            .Concat(Take(ranked, "DEF", formation.Defenders))
            .Concat(Take(ranked, "MID", formation.Midfielders))
            .Concat(Take(ranked, "FWD", formation.Forwards))
            .ToArray();

        return new(
            $"{formation.Defenders}-{formation.Midfielders}-{formation.Forwards}",
            players,
            players.Sum(player => player.RankingScore));
    }

    private static IEnumerable<LineupPlayer> Take(IReadOnlyList<LineupPlayer> players, string position, int count) => players
        .Where(player => player.Position == position)
        .OrderByDescending(player => player.RankingScore)
        .ThenByDescending(player => player.ProjectedPoints)
        .ThenByDescending(player => player.ExpectedMinutes)
        .ThenBy(player => player.PlayerId)
        .Take(count);

    private static IReadOnlyList<LineupPlayer> OrderStartingXi(IEnumerable<LineupPlayer> players) => players
        .OrderBy(player => PositionOrder(player.Position))
        .ThenByDescending(player => player.RankingScore)
        .ThenBy(player => player.PlayerId)
        .Select((player, index) => player with { RecommendedSquadPosition = index + 1 })
        .ToArray();

    private static IReadOnlyList<LineupPlayer> OrderBench(IEnumerable<LineupPlayer> players)
    {
        var ordered = players
            .OrderBy(player => player.Position == "GKP" ? 1 : 0)
            .ThenByDescending(player => player.RankingScore)
            .ThenBy(player => player.PlayerId);

        return ordered.Select((player, index) => player with { RecommendedSquadPosition = index + 12 }).ToArray();
    }

    private static void ValidateSquad(IReadOnlyList<LineupPlayer> players)
    {
        if (players.Count != 15 || players.Select(player => player.PlayerId).Distinct().Count() != 15)
        {
            throw new InvalidOperationException("A valid FPL squad must contain 15 distinct players.");
        }

        var expected = new Dictionary<string, int> { ["GKP"] = 2, ["DEF"] = 5, ["MID"] = 5, ["FWD"] = 3 };
        if (expected.Any(item => players.Count(player => player.Position == item.Key) != item.Value)
            || players.Any(player => !expected.ContainsKey(player.Position)))
        {
            throw new InvalidOperationException("The squad does not have the required FPL position allocation.");
        }
    }

    private static int PositionOrder(string position) => position switch
    {
        "GKP" => 0,
        "DEF" => 1,
        "MID" => 2,
        "FWD" => 3,
        _ => 4
    };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record FormationOption(string Name, IReadOnlyList<LineupPlayer> Players, decimal Score);
}