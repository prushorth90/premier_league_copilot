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
        var fplApiException = exception as FplApiException;
        var isMissingFplData = fplApiException?.StatusCode == System.Net.HttpStatusCode.NotFound;
        var isUpstreamFailure = fplApiException is not null;
        var statusCode = isMissingFplData
            ? StatusCodes.Status404NotFound
            : isUpstreamFailure
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
            Title = isMissingFplData
                ? "The requested FPL data was not found."
                : isUpstreamFailure
                    ? "The Fantasy Premier League service is temporarily unavailable."
                    : "An unexpected error occurred.",
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}