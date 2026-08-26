using Backend.ExternalClients;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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
        var isValidationFailure = exception is ArgumentException or JsonException;
        var isDatabaseFailure = exception is DbUpdateException or NpgsqlException;
        var badHttpRequest = exception as BadHttpRequestException;
        var statusCode = badHttpRequest?.StatusCode ?? (isMissingFplData
            ? StatusCodes.Status404NotFound
            : isValidationFailure
                ? StatusCodes.Status400BadRequest
                : isDatabaseFailure
                    ? StatusCodes.Status503ServiceUnavailable
            : isUpstreamFailure
                ? StatusCodes.Status502BadGateway
                : StatusCodes.Status500InternalServerError);

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
                : badHttpRequest?.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "The request body is too large."
                    : badHttpRequest is not null
                        ? "The HTTP request was invalid."
                : isValidationFailure
                    ? "The request was invalid."
                    : isDatabaseFailure
                        ? "Application storage is temporarily unavailable."
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