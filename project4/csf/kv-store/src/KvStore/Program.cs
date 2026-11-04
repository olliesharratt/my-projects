using System.Net;
using System.Net.Sockets;
using KvStore;

var port = args.Length > 0 ? int.Parse(args[0]) : 6380;
var capacity = args.Length > 1 ? int.Parse(args[1]) : 1000;
var store = new Store(capacity);

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Console.WriteLine($"kv-store listening on :{port} (capacity {capacity}). Try: nc localhost {port}");

_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await timer.WaitForNextTickAsync()) store.Sweep();
});

while (true) _ = Handle(await listener.AcceptTcpClientAsync());

async Task Handle(TcpClient client)
{
    try
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("+BYE"); break; }
                await writer.WriteLineAsync(CommandHandler.Execute(store, line));
            }
        }
    }
    catch (IOException) { /* client disconnected */ }
}
