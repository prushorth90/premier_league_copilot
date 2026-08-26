using Backend.Recommendation.Captain;
using Backend.Recommendation.Captain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/recommendations")]
[Produces("application/json")]
public sealed class RecommendationsController(
    ICaptainRecommendationService captainRecommendationService) : ControllerBase
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
}