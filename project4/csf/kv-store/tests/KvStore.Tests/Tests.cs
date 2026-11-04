using KvStore;
using Xunit;

public class LruCacheTests
{
    [Fact]
    public void EvictsLeastRecentlyUsed()
    {
        var c = new LruCache<string, int>(2);
        var evicted = new List<string>();
        c.Evicted += evicted.Add;
        c.Set("a", 1); c.Set("b", 2);
        c.TryGet("a", out _);          // a is now most recent
        c.Set("c", 3);                 // should evict b
        Assert.Equal(["b"], evicted);
        Assert.Equal(["c", "a"], c.Keys());
    }

    [Fact]
    public void UpdatingExistingKeyDoesNotEvict()
    {
        var c = new LruCache<string, int>(1);
        c.Set("a", 1); c.Set("a", 2);
        Assert.True(c.TryGet("a", out var v));
        Assert.Equal(2, v);
    }
}

public class StoreTests
{
    [Fact]
    public void KeysExpireAfterTtl()
    {
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var s = new Store(10, () => t);
        s.Set("k", "v", 5);
        Assert.Equal("v", s.Get("k"));
        t = t.AddSeconds(6);
        Assert.Null(s.Get("k"));
    }

    [Fact]
    public void SweepRemovesExpiredKeys()
    {
        var t = DateTime.UtcNow;
        var s = new Store(10, () => t);
        s.Set("a", "1", 1); s.Set("b", "2");
        t = t.AddSeconds(2);
        Assert.Equal(1, s.Sweep());
        Assert.Equal(1, s.Count);
    }
}

public class CommandTests
{
    [Fact]
    public void SetGetDelRoundTrip()
    {
        var s = new Store(10);
        Assert.Equal("+OK", CommandHandler.Execute(s, "SET greeting hello world"));
        Assert.Equal("$hello world", CommandHandler.Execute(s, "GET greeting"));
        Assert.Equal(":1", CommandHandler.Execute(s, "DEL greeting"));
        Assert.Equal("$-1", CommandHandler.Execute(s, "GET greeting"));
    }

    [Fact]
    public void SetWithExpiry_ParsesExAndKeepsValue()
    {
        var s = new Store(10);
        CommandHandler.Execute(s, "SET k some value EX 60");
        Assert.Equal("$some value", CommandHandler.Execute(s, "GET k"));
        Assert.Equal(":60", CommandHandler.Execute(s, "TTL k"));
    }

    [Fact]
    public void UnknownCommand_IsAnError() =>
        Assert.StartsWith("-ERR", CommandHandler.Execute(new Store(1), "NOPE"));
}
