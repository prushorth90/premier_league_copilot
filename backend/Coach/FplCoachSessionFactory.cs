using Backend.Coach.Models;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace Backend.Coach;

public sealed class FplCoachSessionFactory(
    IFplCoachFactService factService,
    IPlayerRecommendationService recommendationService) : IFplCoachSessionFactory
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
                Description = "GetUpcomingFixtures(playerId, gameweeks): return official fixtures and deterministic difficulty aggregation for one owned player."
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
        var recommendationTool = CopilotTool.DefineTool(
            async (
                [Description("Exact player ID from CURRENT_FPL_CONTEXT")] int playerId,
                [Description("Decision horizon in gameweeks, from 1 to 5")] int gameweeks,
                [Description("Maximum legal replacement candidates, from 1 to 5")] int candidateLimit) =>
                await recommendationService.GetRecommendationAsync(context, playerId, gameweeks, candidateLimit, cancellationToken),
            factoryOptions: new AIFunctionFactoryOptions
            {
                Name = FplCoachAgents.RecommendationTool,
                Description = "GetPlayerRecommendation(playerId, gameweeks, candidateLimit): return the authoritative deterministic C# HOLD, BENCH, or TRANSFER decision and all supporting specialist data."
            });

        return new SessionConfig
        {
            Model = model,
            Agent = FplCoachAgents.ParentName,
            CustomAgents = FplCoachAgents.Create().ToList(),
            Tools = [availabilityTool, fixturesTool, transfersTool, recommendationTool],
            AvailableTools = new ToolSet()
                .AddBuiltIn("task")
                .AddCustom(FplCoachAgents.AvailabilityTool)
                .AddCustom(FplCoachAgents.FixturesTool)
                .AddCustom(FplCoachAgents.TransfersTool)
                .AddCustom(FplCoachAgents.RecommendationTool),
            OnPermissionRequest = PermissionHandler.ApproveAll
        };
    }
}