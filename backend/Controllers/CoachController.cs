using Backend.Coach;
using Backend.Coach.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/coach")]
[Produces("application/json")]
public sealed class CoachController(ICoachService coachService) : ControllerBase
{
    [HttpPost("chat", Name = "ChatWithCoach")]
    [ProducesResponseType<CoachChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CoachChatResponse>> ChatAsync(
        CoachChatRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (request.TeamId <= 0 || string.IsNullOrWhiteSpace(message) || message.Length > 1_000)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid coach message.",
                detail: "Team ID must be positive and message must contain between 1 and 1000 characters.");
        }

        return Ok(await coachService.ReplyAsync(request.TeamId, message, cancellationToken));
    }
}