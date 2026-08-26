using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain;

public interface ICaptainRankingCalculator
{
    IReadOnlyList<CaptainCandidate> Rank(IEnumerable<CaptainCandidateContext> candidates);
}