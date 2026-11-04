namespace RateLimiting;

public sealed record RateLimitResult(bool Allowed, int Remaining, int Limit, TimeSpan? RetryAfter);

public interface IRateLimiter
{
    /// <summary>Tries to consume <paramref name="permits"/> for <paramref name="key"/> (a user id, API key or IP).</summary>
    RateLimitResult TryAcquire(string key, int permits = 1);
}
