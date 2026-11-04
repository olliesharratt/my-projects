using System.Collections.Concurrent;

namespace RateLimiting;

/// <summary>
/// Sliding window counter: keeps only the current and previous fixed-window counts and weights the previous one by
/// how much of it still overlaps the sliding window. Fixes the fixed-window boundary burst (2x limit across a
/// boundary) with O(1) memory per key, at the cost of being an approximation (assumes requests were evenly spread).
/// </summary>
public sealed class SlidingWindowLimiter : IRateLimiter
{
    private sealed class Window { public long Index = long.MinValue; public int Curr, Prev; }

    private readonly int _limit;
    private readonly long _windowMs;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public SlidingWindowLimiter(int limit, TimeSpan window, TimeProvider? clock = null)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        _limit = limit;
        _windowMs = (long)window.TotalMilliseconds;
        _clock = clock ?? TimeProvider.System;
    }

    public RateLimitResult TryAcquire(string key, int permits = 1)
    {
        if (permits < 1 || permits > _limit) throw new ArgumentOutOfRangeException(nameof(permits));
        var w = _windows.GetOrAdd(key, _ => new Window());
        lock (w)
        {
            var ms = _clock.GetUtcNow().ToUnixTimeMilliseconds();
            var index = ms / _windowMs;
            var fraction = (ms % _windowMs) / (double)_windowMs;       // how far into the current window we are
            if (index != w.Index)
            {
                w.Prev = index == w.Index + 1 ? w.Curr : 0;             // a gap of 2+ windows means the old count is irrelevant
                w.Curr = 0;
                w.Index = index;
            }
            var estimate = w.Prev * (1 - fraction) + w.Curr;
            if (estimate + permits <= _limit)
            {
                w.Curr += permits;
                return new(true, (int)Math.Floor(_limit - estimate - permits), _limit, null);
            }
            // Conservative hint: by the end of this window the previous window no longer counts.
            return new(false, 0, _limit, TimeSpan.FromMilliseconds(_windowMs * (1 - fraction)));
        }
    }
}
