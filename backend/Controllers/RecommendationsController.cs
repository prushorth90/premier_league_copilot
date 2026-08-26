using Backend.Recommendation.Captain;
using Backend.Recommendation.Captain.Models;
using Backend.Recommendation.Lineup;
using Backend.Recommendation.Lineup.Models;
using Backend.Recommendation.Transfer;
using Backend.Recommendation.Transfer.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/recommendations")]
[Produces("application/json")]
public sealed class RecommendationsController(
    ICaptainRecommendationService captainRecommendationService,
    ILineupRecommendationService lineupRecommendationService,
    ITransferRecommendationService transferRecommendationService) : ControllerBase
{
    [HttpGet("{teamId:int}/captain", Name = "GetCaptainRecommendation")]
    [ProducesResponseType<CaptainRecommendation>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CaptainRecommendation>> GetCaptainAsync(
        int teamId,
        CancellationToken cancellationToken)
    {
        if (teamId <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid FPL team ID.",
                detail: "The team ID must be a positive integer.");
        }

        var recommendation = await captainRecommendationService.GetRecommendationAsync(teamId, cancellationToken);
        return Ok(recommendation);
    }

    [HttpGet("{teamId:int}/lineup", Name = "GetLineupRecommendation")]
    [ProducesResponseType<LineupRecommendation>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LineupRecommendation>> GetLineupAsync(
        int teamId,
        CancellationToken cancellationToken)
    {
        if (teamId <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid FPL team ID.",
                detail: "The team ID must be a positive integer.");
        }

        var recommendation = await lineupRecommendationService.GetRecommendationAsync(teamId, cancellationToken);
        return Ok(recommendation);
    }

    [HttpGet("{teamId:int}/transfers", Name = "GetTransferRecommendations")]
    [ProducesResponseType<TransferRecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TransferRecommendationResponse>> GetTransfersAsync(
        int teamId,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (teamId <= 0 || limit is < 1 or > 50)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid transfer recommendation request.",
                detail: "The team ID must be positive and limit must be between 1 and 50.");
        }

        var recommendation = await transferRecommendationService.GetRecommendationsAsync(teamId, limit, cancellationToken);
        return Ok(recommendation);
    }
}