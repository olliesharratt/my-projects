namespace KvStore;

public record Entry(string Value, DateTime? ExpiresAt);

/// <summary>Thread-safe store: LRU eviction when full, lazy expiry on access plus a periodic Sweep.</summary>
public class Store(int capacity, Func<DateTime>? now = null)
{
    private readonly LruCache<string, Entry> _cache = new(capacity);
    private readonly Func<DateTime> _now = now ?? (() => DateTime.UtcNow);
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _cache.Count; } }

    public void Set(string key, string value, int? ttlSeconds = null)
    {
        lock (_lock) _cache.Set(key, new Entry(value, ttlSeconds is null ? null : _now().AddSeconds(ttlSeconds.Value)));
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            if (!_cache.TryGet(key, out var e)) return null;
            if (Expired(e)) { _cache.Remove(key); return null; }
            return e.Value;
        }
    }

    public bool Del(string key) { lock (_lock) return _cache.Remove(key); }

    /// <summary>-2 = no such key, -1 = no expiry, otherwise seconds remaining.</summary>
    public int Ttl(string key)
    {
        lock (_lock)
        {
            if (!_cache.TryGet(key, out var e)) return -2;
            if (Expired(e)) { _cache.Remove(key); return -2; }
            return e.ExpiresAt is null ? -1 : (int)Math.Ceiling((e.ExpiresAt.Value - _now()).TotalSeconds);
        }
    }

    public int Sweep()
    {
        lock (_lock)
        {
            var dead = _cache.Keys().Where(k => { _cache.TryGet(k, out var e); return Expired(e); }).ToList();
            foreach (var k in dead) _cache.Remove(k);
            return dead.Count;
        }
    }

    private bool Expired(Entry e) => e.ExpiresAt is { } t && t <= _now();
}
