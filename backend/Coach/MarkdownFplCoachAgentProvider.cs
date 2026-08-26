namespace Backend.Coach;

public sealed class MarkdownFplCoachAgentProvider : IFplCoachAgentProvider
{
    private static readonly AgentDefinition[] Definitions =
    [
        new("fpl-coach.agent.md", FplCoachAgents.ParentName, "FPL Coach", ["task"], false),
        new("injury.agent.md", FplCoachAgents.InjurySpecialistName, "Injury Agent", [FplCoachAgents.AvailabilityTool], true),
        new("fixture.agent.md", FplCoachAgents.FixtureSpecialistName, "Fixture Agent", [FplCoachAgents.FixturesTool], true),
        new("transfer.agent.md", FplCoachAgents.TransferSpecialistName, "Transfer Agent", [FplCoachAgents.TransfersTool], true)
    ];

    private readonly IReadOnlyList<FplCoachAgentDefinition> agents;

    public MarkdownFplCoachAgentProvider(
        IHostEnvironment environment,
        ILogger<MarkdownFplCoachAgentProvider> logger)
        : this(ResolveAgentDirectory(environment.ContentRootPath), logger)
    {
    }

    public MarkdownFplCoachAgentProvider(
        string agentDirectory,
        ILogger<MarkdownFplCoachAgentProvider>? logger = null)
    {
        agents = Definitions.Select(definition =>
        {
            var agent = Load(agentDirectory, definition);
            logger?.LogInformation(
                "Loaded Copilot agent file {AgentFile} as {AgentName} with tools {AgentTools}",
                Path.Combine(agentDirectory, definition.FileName),
                agent.Name,
                agent.Tools.Count == 0 ? "none" : string.Join(",", agent.Tools));
            return agent;
        }).ToArray();
    }

    public IReadOnlyList<FplCoachAgentDefinition> GetAgents() => agents;

    private static FplCoachAgentDefinition Load(string directory, AgentDefinition definition)
    {
        var path = Path.Combine(directory, definition.FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required Copilot agent file '{definition.FileName}' was not found in '{directory}'.");
        }

        var document = ParseDocument(File.ReadAllText(path), definition.FileName);
        if (!document.Values.TryGetValue("name", out var name) || name != definition.Name)
        {
            throw new InvalidOperationException($"Agent file '{definition.FileName}' must declare name '{definition.Name}'.");
        }

        if (!document.Values.TryGetValue("description", out var description) || string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException($"Agent file '{definition.FileName}' must declare a description.");
        }

        if (!document.Values.TryGetValue("tools", out var toolsValue))
        {
            throw new InvalidOperationException($"Agent file '{definition.FileName}' must declare its restricted tools.");
        }

        var tools = ParseTools(toolsValue);
        if (!tools.SequenceEqual(definition.Tools, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Agent file '{definition.FileName}' must use exactly these tools: {string.Join(", ", definition.Tools)}.");
        }

        if (string.IsNullOrWhiteSpace(document.Prompt))
        {
            throw new InvalidOperationException($"Agent file '{definition.FileName}' must contain Markdown instructions.");
        }

        return new FplCoachAgentDefinition(
            definition.Name,
            definition.DisplayName,
            description,
            tools,
            definition.Infer,
            document.Prompt);
    }

    private static AgentDocument ParseDocument(string content, string fileName)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Agent file '{fileName}' must start with YAML frontmatter.");
        }

        var closingDelimiter = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (closingDelimiter < 0)
        {
            throw new InvalidOperationException($"Agent file '{fileName}' has unterminated YAML frontmatter.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in normalized[4..closingDelimiter].Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = Unquote(line[(separator + 1)..].Trim());
            if (!values.TryAdd(key, value))
            {
                throw new InvalidOperationException($"Agent file '{fileName}' declares '{key}' more than once.");
            }
        }

        return new AgentDocument(values, normalized[(closingDelimiter + 5)..].Trim());
    }

    private static string[] ParseTools(string value)
    {
        if (value.Length < 2 || value[0] != '[' || value[^1] != ']')
        {
            return [];
        }

        return value[1..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Unquote)
            .ToArray();
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    private static string ResolveAgentDirectory(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(contentRootPath, "..", ".github", "agents")),
            Path.Combine(AppContext.BaseDirectory, "Agents")
        };
        return candidates.FirstOrDefault(Directory.Exists)
            ?? throw new InvalidOperationException(
                $"Copilot agent directory was not found. Checked: {string.Join(", ", candidates)}.");
    }

    private sealed record AgentDefinition(
        string FileName,
        string Name,
        string DisplayName,
        string[] Tools,
        bool Infer);

    private sealed record AgentDocument(
        IReadOnlyDictionary<string, string> Values,
        string Prompt);
}