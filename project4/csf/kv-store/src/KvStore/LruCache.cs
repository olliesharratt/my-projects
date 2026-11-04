namespace KvStore;

/// <summary>LRU cache: Dictionary for O(1) lookup + hand-written doubly linked list for O(1) recency updates.</summary>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private sealed class Node
    {
        public TKey Key = default!;
        public TValue Value = default!;
        public Node? Prev, Next;
    }

    private readonly Dictionary<TKey, Node> _map = new();
    private readonly Node _head = new(), _tail = new(); // sentinels: head.Next is most recent, tail.Prev is least recent

    public LruCache(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _head.Next = _tail;
        _tail.Prev = _head;
    }

    public int Capacity { get; }
    public int Count => _map.Count;
    public event Action<TKey>? Evicted;

    public bool TryGet(TKey key, out TValue value)
    {
        if (_map.TryGetValue(key, out var n)) { Detach(n); AddFront(n); value = n.Value; return true; }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            existing.Value = value; Detach(existing); AddFront(existing);
            return;
        }
        if (_map.Count == Capacity)
        {
            var lru = _tail.Prev!;
            Detach(lru); _map.Remove(lru.Key); Evicted?.Invoke(lru.Key);
        }
        var n = new Node { Key = key, Value = value };
        _map[key] = n; AddFront(n);
    }

    public bool Remove(TKey key)
    {
        if (!_map.Remove(key, out var n)) return false;
        Detach(n);
        return true;
    }

    /// <summary>Keys from most to least recently used.</summary>
    public IEnumerable<TKey> Keys()
    {
        for (var n = _head.Next; n != _tail; n = n!.Next) yield return n!.Key;
    }

    private void Detach(Node n) { n.Prev!.Next = n.Next; n.Next!.Prev = n.Prev; }
    private void AddFront(Node n)
    {
        n.Next = _head.Next; n.Prev = _head;
        _head.Next!.Prev = n; _head.Next = n;
    }
}
