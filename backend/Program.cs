using Backend.Configuration;
using Backend.Coach;
using Backend.ExternalClients;
using Backend.Middleware;
using Backend.Persistence;
using Backend.Recommendation;
using Backend.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new();

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = securityOptions.MaxRequestBodyKilobytes * 1024L);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddApplicationConfiguration(builder.Configuration);
builder.Services.AddCoachServices();
builder.Services.AddApplicationPersistence(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddExternalClients();
builder.Services.AddRecommendationServices();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection(AppCorsOptions.SectionName)
        .Get<AppCorsOptions>()?.AllowedOrigins ?? [];

    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return context.Request.Path == "/health"
            ? RateLimitPartition.GetNoLimiter(client)
            : RateLimitPartition.GetFixedWindowLimiter(
                client,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = securityOptions.RequestLimitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "Wait briefly before trying again."
        }, cancellationToken);
    };
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.Services.ApplyApplicationMigrationsAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

if (app.Services.GetRequiredService<IOptions<SecurityOptions>>().Value.UseHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
