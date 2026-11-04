using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace RateLimiting;

/// <summary>Rejects requests over the limit with 429 and Retry-After; adds X-RateLimit-* headers to every response.</summary>
public sealed class RateLimitMiddleware(RequestDelegate next, IRateLimiter limiter, Func<HttpContext, string> keySelector)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var result = limiter.TryAcquire(keySelector(context));
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        if (!result.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (result.RetryAfter is { } wait)
                context.Response.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString();
            await context.Response.WriteAsync("Too Many Requests");
            return;
        }
        await next(context);
    }
}

public static class RateLimitExtensions
{
    /// <summary>Default key is the client IP. Behind a proxy, supply a selector that reads a trusted forwarded header or API key.</summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, IRateLimiter limiter, Func<HttpContext, string>? keySelector = null) =>
        app.UseMiddleware<RateLimitMiddleware>(limiter, keySelector ?? (ctx => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
}
