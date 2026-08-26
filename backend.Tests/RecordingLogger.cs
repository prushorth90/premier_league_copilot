using Microsoft.Extensions.Logging;

namespace Backend.Tests;

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception));
}

internal sealed class TestAgentProvider : Backend.Coach.IFplCoachAgentProvider
{
    public IReadOnlyList<Backend.Coach.FplCoachAgentDefinition> GetAgents() =>
    [
        new(Backend.Coach.FplCoachAgents.ParentName, "FPL Coach", "Parent", ["task"], false, "Parent instructions"),
        new(Backend.Coach.FplCoachAgents.InjurySpecialistName, "Injury Agent", "Injury", [Backend.Coach.FplCoachAgents.AvailabilityTool], true, "Injury instructions"),
        new(Backend.Coach.FplCoachAgents.FixtureSpecialistName, "Fixture Agent", "Fixture", [Backend.Coach.FplCoachAgents.FixturesTool], true, "Fixture instructions"),
        new(Backend.Coach.FplCoachAgents.TransferSpecialistName, "Transfer Agent", "Transfer", [Backend.Coach.FplCoachAgents.TransfersTool], true, "Transfer instructions")
    ];
}