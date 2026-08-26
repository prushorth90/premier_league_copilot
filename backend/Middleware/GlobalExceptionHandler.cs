using Backend.ExternalClients;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isUpstreamFailure = exception is FplApiException;
        var statusCode = isUpstreamFailure
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status500InternalServerError;

        logger.Log(
            isUpstreamFailure ? LogLevel.Warning : LogLevel.Error,
            exception,
            "Request {RequestMethod} {RequestPath} failed with status {StatusCode}. Trace ID: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            httpContext.TraceIdentifier);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = isUpstreamFailure
                ? "The Fantasy Premier League service is temporarily unavailable."
                : "An unexpected error occurred.",
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}