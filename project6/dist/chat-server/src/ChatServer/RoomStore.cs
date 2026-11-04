using System.Collections.Concurrent;

namespace ChatServer;

public record ChatMessage(long Seq, string Room, string User, string Text, DateTimeOffset At);

/// <summary>Per-room bounded history. Every message gets a per-room, strictly increasing sequence number.</summary>
public sealed class RoomStore(int maxHistory = 100)
{
    private sealed class Room
    {
        public readonly SemaphoreSlim Gate = new(1, 1);   // serialises "assign seq + broadcast" so clients see seq order
        public readonly object Lock = new();
        public long Next = 1;
        public readonly List<ChatMessage> Messages = [];
    }

    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public async Task<ChatMessage> PostAsync(string room, string user, string text, Func<ChatMessage, Task> broadcast)
    {
        var r = _rooms.GetOrAdd(room, _ => new Room());
        await r.Gate.WaitAsync();
        try
        {
            ChatMessage m;
            lock (r.Lock)
            {
                m = new ChatMessage(r.Next++, room, user, text, DateTimeOffset.UtcNow);
                r.Messages.Add(m);
                if (r.Messages.Count > maxHistory) r.Messages.RemoveAt(0);
            }
            await broadcast(m);
            return m;
        }
        finally { r.Gate.Release(); }
    }

    /// <summary>Messages with Seq greater than <paramref name="afterSeq"/>: what a reconnecting client missed.</summary>
    public IReadOnlyList<ChatMessage> Since(string room, long afterSeq)
    {
        if (!_rooms.TryGetValue(room, out var r)) return [];
        lock (r.Lock) return r.Messages.Where(m => m.Seq > afterSeq).ToList();
    }
}

/// <summary>Who is in which room, keyed by connection so one user with two tabs appears once and leaves cleanly.</summary>
public sealed class PresenceTracker
{
    private readonly Dictionary<string, Dictionary<string, string>> _rooms = new();   // room -> connectionId -> user
    private readonly object _lock = new();

    public void Join(string room, string connectionId, string user)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(room, out var d)) _rooms[room] = d = new();
            d[connectionId] = user;
        }
    }

    public bool Leave(string room, string connectionId)
    {
        lock (_lock) return _rooms.TryGetValue(room, out var d) && d.Remove(connectionId);
    }

    /// <summary>Removes a connection from every room; returns the rooms whose membership changed.</summary>
    public List<string> LeaveAll(string connectionId)
    {
        lock (_lock)
        {
            var affected = new List<string>();
            foreach (var (room, d) in _rooms) if (d.Remove(connectionId)) affected.Add(room);
            return affected;
        }
    }

    public bool IsIn(string room, string connectionId)
    {
        lock (_lock) return _rooms.TryGetValue(room, out var d) && d.ContainsKey(connectionId);
    }

    public List<string> Users(string room)
    {
        lock (_lock) return _rooms.TryGetValue(room, out var d) ? d.Values.Distinct().Order().ToList() : [];
    }
}
