namespace Backend.Middleware;

public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.Append("Referrer-Policy", "no-referrer");
            headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()"
            );
            headers.Append("Cross-Origin-Opener-Policy", "same-origin");
            if (!environment.IsDevelopment())
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}