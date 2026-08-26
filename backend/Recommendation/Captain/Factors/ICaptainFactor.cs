using Backend.Recommendation.Captain.Models;

namespace Backend.Recommendation.Captain.Factors;

public interface ICaptainFactor
{
    int Order { get; }

    string Name { get; }

    CaptainFactorBreakdown Calculate(CaptainCandidateContext context);
}