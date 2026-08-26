using System.Diagnostics;

namespace Backend.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = context.TraceIdentifier
        });

        try
        {
            await next(context);
        }
        finally
        {
            if (context.Request.Path == "/health")
            {
                logger.LogDebug(
                    "HTTP health probe completed with {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "HTTP {RequestMethod} {RequestPath} completed with {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}