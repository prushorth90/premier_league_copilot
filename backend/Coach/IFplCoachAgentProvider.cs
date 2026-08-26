namespace Backend.Coach;

public interface IFplCoachAgentProvider
{
    IReadOnlyList<FplCoachAgentDefinition> GetAgents();
}

public sealed record FplCoachAgentDefinition(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Tools,
    bool Infer,
    string Instructions);