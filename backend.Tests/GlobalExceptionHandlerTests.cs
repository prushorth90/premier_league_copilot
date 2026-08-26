using System.Text.Json;
using Backend.ExternalClients;
using Backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task TryHandleAsyncReturnsNotFoundForMissingFplData()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "missing-trace-id"
        };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            httpContext,
            new FplApiException("entry/999/", System.Net.HttpStatusCode.NotFound),
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(httpContext.Response.Body);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal(
            "The requested FPL data was not found.",
            response.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("validation", StatusCodes.Status400BadRequest, "The request was invalid.")]
    [InlineData("database", StatusCodes.Status503ServiceUnavailable, "Application storage is temporarily unavailable.")]
    public async Task TryHandleAsyncClassifiesApplicationFailures(string failure, int expectedStatus, string expectedTitle)
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "classified-trace-id" };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        Exception exception = failure == "validation"
            ? new ArgumentException("Sensitive validation detail")
            : new DbUpdateException("Sensitive database detail");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(httpContext.Response.Body);
        Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
        Assert.Equal(expectedTitle, response.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Sensitive", response.RootElement.GetRawText());
    }

    [Fact]
    public async Task TryHandleAsyncPreservesPayloadTooLargeStatus()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "payload-trace-id" };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            httpContext,
            new BadHttpRequestException("Sensitive request detail", StatusCodes.Status413PayloadTooLarge),
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(httpContext.Response.Body);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, httpContext.Response.StatusCode);
        Assert.Equal("The request body is too large.", response.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Sensitive", response.RootElement.GetRawText());
    }
}