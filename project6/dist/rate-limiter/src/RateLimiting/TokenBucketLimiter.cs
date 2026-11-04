using System.Collections.Concurrent;

namespace RateLimiting;

/// <summary>
/// Token bucket: each key has a bucket holding up to <c>capacity</c> tokens that refills continuously at
/// <c>refillPerSecond</c>. Allows bursts up to capacity, then a steady average rate. O(1) time and memory per key.
/// </summary>
public sealed class TokenBucketLimiter : IRateLimiter
{
    private sealed class Bucket { public double Tokens; public DateTimeOffset Last; }

    private readonly int _capacity;
    private readonly double _refillPerSecond;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    public TokenBucketLimiter(int capacity, double refillPerSecond, TimeProvider? clock = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (refillPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillPerSecond));
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
        _clock = clock ?? TimeProvider.System;
    }

    public RateLimitResult TryAcquire(string key, int permits = 1)
    {
        if (permits < 1 || permits > _capacity) throw new ArgumentOutOfRangeException(nameof(permits));
        var bucket = _buckets.GetOrAdd(key, _ => new Bucket { Tokens = _capacity, Last = _clock.GetUtcNow() });
        lock (bucket)
        {
            // Lazy refill: compute tokens earned since last touch instead of running a timer per bucket.
            var now = _clock.GetUtcNow();
            var elapsed = (now - bucket.Last).TotalSeconds;
            if (elapsed > 0)
            {
                bucket.Tokens = Math.Min(_capacity, bucket.Tokens + elapsed * _refillPerSecond);
                bucket.Last = now;
            }
            if (bucket.Tokens >= permits)
            {
                bucket.Tokens -= permits;
                return new(true, (int)bucket.Tokens, _capacity, null);
            }
            var wait = TimeSpan.FromSeconds((permits - bucket.Tokens) / _refillPerSecond);
            return new(false, (int)bucket.Tokens, _capacity, wait);
        }
    }

    /// <summary>Drops buckets untouched for <paramref name="idle"/>. An idle bucket would be full anyway, so this loses nothing.</summary>
    public int Sweep(TimeSpan idle)
    {
        var cutoff = _clock.GetUtcNow() - idle;
        var removed = 0;
        foreach (var (key, bucket) in _buckets)
            if (bucket.Last < cutoff && _buckets.TryRemove(key, out _)) removed++;
        return removed;
    }

    public int TrackedKeys => _buckets.Count;
}
