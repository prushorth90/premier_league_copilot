using Backend.Recommendation.Captain.Factors;
using Backend.Recommendation.Captain.Models;
using Backend.Models;

namespace Backend.Recommendation.Captain;

public sealed class CaptainRankingCalculator(
    IEnumerable<ICaptainFactor> factors) : ICaptainRankingCalculator
{
    private readonly IReadOnlyList<ICaptainFactor> orderedFactors = factors.OrderBy(item => item.Order).ToArray();

    public IReadOnlyList<CaptainCandidate> Rank(IEnumerable<CaptainCandidateContext> candidates) => candidates
        .Select(CreateCandidate)
        .OrderByDescending(candidate => candidate.RankingScore)
        .ThenByDescending(candidate => candidate.ProjectedPoints)
        .ThenBy(candidate => candidate.PlayerId)
        .ToArray();

    private CaptainCandidate CreateCandidate(CaptainCandidateContext context)
    {
        var breakdown = orderedFactors
            .Select(factor => factor.Calculate(context))
            .Select(item => item with { Score = Round(item.Score) })
            .ToArray();
        var projectedPoints = context.Projection.Horizons.Single(item => item.Gameweeks == 1).ProjectedPoints;

        return new(
            context.Player.Id,
            context.Player.DisplayName,
            context.TeamName,
            context.Position,
            projectedPoints,
            Round(breakdown.Sum(item => item.Score)),
            breakdown,
            PlayerPhotoUrl.FromCode(context.Player.Code));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}