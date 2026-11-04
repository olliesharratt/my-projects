using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using RaftLite;

// Usage: RaftLite <id> <port> <peerId=host:port>...
if (args.Length < 3) { Console.Error.WriteLine("Usage: RaftLite <id> <port> <peerId=host:port>..."); return 64; }
var id = args[0];
var peers = args.Skip(2).Select(a => a.Split('=', 2)).ToDictionary(p => p[0], p => IPEndPoint.Parse(p[1]));

var udp = new UdpClient(int.Parse(args[1]));
var inbox = Channel.CreateUnbounded<(string From, IMessage Message)>();

// Receive on a background task, but hand messages to the single node thread through a channel: the node is not thread-safe.
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            var result = await udp.ReceiveAsync();
            if (Codec.Decode(Encoding.UTF8.GetString(result.Buffer)) is { } d) inbox.Writer.TryWrite(d);
        }
        catch (SocketException) { /* ICMP "port unreachable" from a dead peer: ignore */ }
    }
});

var clock = Stopwatch.StartNew();
var node = new RaftNode(id, peers.Keys.ToList(), new UdpTransport(id, udp, peers), new Random(), clock.Elapsed);
node.Log += Console.WriteLine;
Console.WriteLine($"[{id}] up on port {args[1]} with peers {string.Join(", ", peers.Keys)}");

while (true)
{
    while (inbox.Reader.TryRead(out var m)) node.Receive(m.From, m.Message, clock.Elapsed);
    node.Tick(clock.Elapsed);
    await Task.Delay(10);
}

sealed class UdpTransport(string self, UdpClient udp, Dictionary<string, IPEndPoint> peers) : IRaftTransport
{
    public void Send(string to, IMessage message)
    {
        var bytes = Encoding.UTF8.GetBytes(Codec.Encode(self, message));
        try { udp.Send(bytes, bytes.Length, peers[to]); }
        catch (SocketException) { }       // Raft tolerates lost messages; so do we
    }
}
