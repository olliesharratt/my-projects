using Microsoft.AspNetCore.Http;
using RateLimiting;
using Xunit;

public sealed class ManualTime : TimeProvider
{
    // Starts on a whole-second boundary so sliding-window maths is easy to reason about.
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch.AddSeconds(1000);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan t) => _now += t;
}

public class TokenBucketTests
{
    [Fact]
    public void AllowsBurstThenRejectsWithRetryAfter()
    {
        var l = new TokenBucketLimiter(5, 1, new ManualTime());
        for (var i = 0; i < 5; i++) Assert.True(l.TryAcquire("a").Allowed);
        var r = l.TryAcquire("a");
        Assert.False(r.Allowed);
        Assert.Equal(1.0, r.RetryAfter!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void RefillsOverTimeButNeverAboveCapacity()
    {
        var clock = new ManualTime();
        var l = new TokenBucketLimiter(5, 1, clock);
        for (var i = 0; i < 5; i++) l.TryAcquire("a");
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(l.TryAcquire("a").Allowed);
        Assert.True(l.TryAcquire("a").Allowed);
        Assert.False(l.TryAcquire("a").Allowed);

        clock.Advance(TimeSpan.FromHours(1));
        for (var i = 0; i < 5; i++) Assert.True(l.TryAcquire("a").Allowed);
        Assert.False(l.TryAcquire("a").Allowed);
    }

    [Fact]
    public void KeysAreIndependent()
    {
        var l = new TokenBucketLimiter(1, 1, new ManualTime());
        Assert.True(l.TryAcquire("a").Allowed);
        Assert.False(l.TryAcquire("a").Allowed);
        Assert.True(l.TryAcquire("b").Allowed);
    }

    [Fact]
    public void ConcurrentCallersNeverExceedCapacity()
    {
        var l = new TokenBucketLimiter(100, 0.001, new ManualTime());   // frozen clock: no refill
        var allowed = 0;
        Parallel.For(0, 1000, _ => { if (l.TryAcquire("k").Allowed) Interlocked.Increment(ref allowed); });
        Assert.Equal(100, allowed);
    }

    [Fact]
    public void SweepRemovesIdleBuckets()
    {
        var clock = new ManualTime();
        var l = new TokenBucketLimiter(5, 1, clock);
        l.TryAcquire("old");
        clock.Advance(TimeSpan.FromMinutes(10));
        l.TryAcquire("fresh");
        Assert.Equal(1, l.Sweep(TimeSpan.FromMinutes(5)));
        Assert.Equal(1, l.TrackedKeys);
    }
}

public class SlidingWindowTests
{
    [Fact]
    public void EnforcesLimitWithinWindow()
    {
        var l = new SlidingWindowLimiter(10, TimeSpan.FromSeconds(1), new ManualTime());
        for (var i = 0; i < 10; i++) Assert.True(l.TryAcquire("a").Allowed);
        Assert.False(l.TryAcquire("a").Allowed);
    }

    [Fact]
    public void PreviousWindowFadesOutGradually()
    {
        var clock = new ManualTime();
        var l = new SlidingWindowLimiter(10, TimeSpan.FromSeconds(1), clock);
        for (var i = 0; i < 10; i++) l.TryAcquire("a");

        clock.Advance(TimeSpan.FromSeconds(1));                 // new window, but the old one still counts fully
        Assert.False(l.TryAcquire("a").Allowed);

        clock.Advance(TimeSpan.FromMilliseconds(500));          // half the old window has slid out: ~5 slots free
        var allowed = 0;
        while (l.TryAcquire("a").Allowed) allowed++;
        Assert.Equal(5, allowed);
    }

    [Fact]
    public void LongIdleGapResetsCounts()
    {
        var clock = new ManualTime();
        var l = new SlidingWindowLimiter(3, TimeSpan.FromSeconds(1), clock);
        for (var i = 0; i < 3; i++) l.TryAcquire("a");
        clock.Advance(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 3; i++) Assert.True(l.TryAcquire("a").Allowed);
    }
}

public class MiddlewareTests
{
    [Fact]
    public async Task Returns429WithHeadersWhenLimitExceeded()
    {
        var limiter = new TokenBucketLimiter(1, 1, new ManualTime());
        var nextCalls = 0;
        var mw = new RateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; }, limiter, _ => "client");

        var first = new DefaultHttpContext();
        await mw.InvokeAsync(first);
        Assert.Equal(200, first.Response.StatusCode);
        Assert.Equal("0", first.Response.Headers["X-RateLimit-Remaining"].ToString());

        var second = new DefaultHttpContext();
        await mw.InvokeAsync(second);
        Assert.Equal(429, second.Response.StatusCode);
        Assert.Equal("1", second.Response.Headers["Retry-After"].ToString());
        Assert.Equal(1, nextCalls);                              // the rejected request never reached the app
    }
}
