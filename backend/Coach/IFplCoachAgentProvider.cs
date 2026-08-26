using GitHub.Copilot;

namespace Backend.Coach;

public interface IFplCoachAgentProvider
{
    IReadOnlyList<CustomAgentConfig> GetAgents();
}