using Backend.Coach;
using Backend.Coach.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Backend.Controllers;

[ApiController]
[Route("api/coach")]
[Produces("application/json")]
public sealed class CoachController(ICoachService coachService) : ControllerBase
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

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

    [HttpPost("chat/stream", Name = "StreamCoachChat")]
    [Produces("text/event-stream", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task StreamChatAsync(
        CoachChatRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (request.TeamId <= 0 || string.IsNullOrWhiteSpace(message) || message.Length > 1_000)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid coach message.",
                Detail = "Team ID must be positive and message must contain between 1 and 1000 characters."
            }, cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        await Response.StartAsync(cancellationToken);
        var progressSink = new SseCoachProgressSink(Response);

        try
        {
            var result = await coachService.ReplyWithProgressAsync(
                request.TeamId,
                message,
                progressSink,
                cancellationToken);
            await progressSink.WriteEventAsync("complete", result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            await progressSink.WriteEventAsync(
                "error",
                new { message = "The coach could not complete this request. Try again." },
                cancellationToken);
        }
    }

    private sealed class SseCoachProgressSink(HttpResponse response) : ICoachProgressSink
    {
        public ValueTask ReportAsync(CoachProgressUpdate update, CancellationToken cancellationToken) =>
            new(WriteEventAsync("progress", update, cancellationToken));

        public async Task WriteEventAsync<T>(
            string eventName,
            T payload,
            CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload, StreamJsonOptions);
            await response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}