using Backend.Coach.Models;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace Backend.Coach;

public sealed class FplCoachSessionFactory(IFplCoachFactService factService) : IFplCoachSessionFactory
{
    public SessionConfig Create(
        FplCoachContext context,
        string model,
        CancellationToken cancellationToken)
    {
        var availabilityTool = CopilotTool.DefineTool(
            ([Description("Exact or partial player name from the connected squad")] string playerName) =>
                factService.GetPlayerAvailability(context, playerName),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.AvailabilityTool,
                Description = "Return official FPL availability facts for one owned player."
            });
        var fixturesTool = CopilotTool.DefineTool(
            async (
                [Description("Exact or partial player name from the connected squad")] string playerName,
                [Description("Number of upcoming gameweeks, from 1 to 5")] int gameweeks) =>
                await factService.GetUpcomingFixturesAsync(context, playerName, gameweeks, cancellationToken),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.FixturesTool,
                Description = "Return official upcoming FPL fixtures for one owned player."
            });
        var transfersTool = CopilotTool.DefineTool(
            async (
                [Description("Exact or partial player name from the connected squad")] string playerName,
                [Description("Maximum replacement options, from 1 to 10")] int limit) =>
                await factService.GetTransferOptionsAsync(context, playerName, limit, cancellationToken),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.TransfersTool,
                Description = "Return valid replacement recommendations for one owned player."
            });

        return new SessionConfig
        {
            Model = model,
            Agent = FplCoachAgents.ParentName,
            CustomAgents = FplCoachAgents.Create().ToList(),
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