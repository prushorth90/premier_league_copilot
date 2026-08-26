using Backend.Recommendation.Lineup.Models;

namespace Backend.Recommendation.Lineup;

public interface ILineupOptimizer
{
    (string Formation, IReadOnlyList<LineupPlayer> StartingXi, IReadOnlyList<LineupPlayer> Bench, IReadOnlyList<LineupChange> Changes) Optimize(
        IEnumerable<LineupCandidateContext> candidates);
}