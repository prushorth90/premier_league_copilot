using Backend.Coach.Models;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace Backend.Coach;

public sealed class FplCoachSessionFactory(
    IFplCoachFactService factService,
    IFplCoachAgentProvider agentProvider) : IFplCoachSessionFactory
{
    public SessionConfig Create(
        FplCoachContext context,
        string model,
        CancellationToken cancellationToken)
    {
        var availabilityTool = CopilotTool.DefineTool(
            ([Description("Exact player ID from CURRENT_FPL_CONTEXT")] int playerId) =>
                factService.GetPlayerAvailability(context, playerId),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.AvailabilityTool,
                Description = "GetPlayerAvailability(playerId): return structured official FPL availability facts for one owned player."
            });
        var fixturesTool = CopilotTool.DefineTool(
            async (
                [Description("Exact player ID from CURRENT_FPL_CONTEXT")] int playerId,
                [Description("Number of upcoming gameweeks, from 1 to 5")] int gameweeks) =>
                await factService.GetUpcomingFixturesAsync(context, playerId, gameweeks, cancellationToken),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.FixturesTool,
                Description = "GetUpcomingFixtures(playerId, gameweeks): return official fixtures and deterministic C# difficulty aggregation for one owned player; FixtureAgent calls it for 1, 3, and 5 gameweeks."
            });
        var transfersTool = CopilotTool.DefineTool(
            async (
                [Description("Exact player ID from CURRENT_FPL_CONTEXT")] int playerId,
                [Description("Maximum replacement options, from 1 to 5")] int limit) =>
                await factService.GetTransferCandidatesAsync(context, playerId, limit, cancellationToken),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.TransfersTool,
                Description = "GetTransferCandidates(playerId, limit): return ranked replacements after C# enforces squad, bank, price, position, projection, and three-player club constraints."
            });
        return new SessionConfig
        {
            Model = model,
            Agent = FplCoachAgents.ParentName,
            CustomAgents = agentProvider.GetAgents().ToList(),
            Tools = [availabilityTool, fixturesTool, transfersTool],
            AvailableTools = new ToolSet()
                .AddBuiltIn("task")
                .AddCustom(FplCoachAgents.AvailabilityTool)
                .AddCustom(FplCoachAgents.FixturesTool)
                .AddCustom(FplCoachAgents.TransfersTool),
            OnPermissionRequest = PermissionHandler.ApproveAll
        };
    }
}