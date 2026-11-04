using RaftLite;

/// <summary>
/// Deterministic simulator: virtual 1 ms clock, random message latency, crashes and network partitions.
/// Same seed, same run, so any failing scenario can be replayed exactly.
/// </summary>
public sealed class SimCluster
{
    private readonly PriorityQueue<(string From, string To, IMessage Msg), TimeSpan> _inFlight = new();
    private readonly Random _latency;
    private readonly HashSet<(string, string)> _cut = [];
    public Dictionary<string, RaftNode> Nodes { get; } = new();
    public HashSet<string> Down { get; } = [];
    public Dictionary<int, HashSet<string>> LeadersByTerm { get; } = new();
    public TimeSpan Now { get; private set; }

    public SimCluster(int size, int seed)
    {
        _latency = new Random(seed);
        var ids = Enumerable.Range(1, size).Select(i => $"n{i}").ToList();
        for (var i = 0; i < size; i++)
            Nodes[ids[i]] = new RaftNode(ids[i], ids.Where(x => x != ids[i]).ToList(), new SimTransport(this, ids[i]), new Random(seed * 1000 + i), Now);
    }

    private sealed class SimTransport(SimCluster c, string from) : IRaftTransport
    {
        public void Send(string to, IMessage m)
        {
            if (c.Down.Contains(from)) return;
            c._inFlight.Enqueue((from, to, m), c.Now + TimeSpan.FromMilliseconds(c._latency.Next(1, 10)));
        }
    }

    public void Partition(IEnumerable<string> a, IEnumerable<string> b)
    {
        foreach (var x in a) foreach (var y in b) { _cut.Add((x, y)); _cut.Add((y, x)); }
    }
    public void Heal() => _cut.Clear();

    public IEnumerable<RaftNode> Leaders => Nodes.Values.Where(n => !Down.Contains(n.Id) && n.Role == Role.Leader);

    public void Run(TimeSpan duration)
    {
        var end = Now + duration;
        while (Now < end)
        {
            Now += TimeSpan.FromMilliseconds(1);
            while (_inFlight.TryPeek(out var item, out var due) && due <= Now)
            {
                _inFlight.Dequeue();
                if (Down.Contains(item.To) || _cut.Contains((item.From, item.To))) continue;   // dropped
                Nodes[item.To].Receive(item.From, item.Msg, Now);
            }
            foreach (var n in Nodes.Values)
            {
                if (Down.Contains(n.Id)) continue;
                n.Tick(Now);
                if (n.Role == Role.Leader)
                {
                    if (!LeadersByTerm.TryGetValue(n.Term, out var set)) LeadersByTerm[n.Term] = set = [];
                    set.Add(n.Id);
                }
            }
        }
    }
}
