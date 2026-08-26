using System.Text.Json;
using Backend.ExternalClients;
using Backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsyncReturnsProblemDetailsWithTraceId()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        httpContext.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Sensitive detail"),
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(httpContext.Response.Body);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("An unexpected error occurred.", response.RootElement.GetProperty("title").GetString());
        Assert.Equal("test-trace-id", response.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("Sensitive detail", response.RootElement.GetRawText());
    }

    [Fact]
    public async Task TryHandleAsyncReturnsBadGatewayForFplApiFailure()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "upstream-trace-id"
        };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            httpContext,
            new FplApiException("fixtures/", System.Net.HttpStatusCode.ServiceUnavailable),
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(httpContext.Response.Body);

        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(
            "The Fantasy Premier League service is temporarily unavailable.",
            response.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("fixtures", response.RootElement.GetRawText());
    }
}