using System.Net;
using System.Net.Sockets;

namespace TinyHttp;

/// <summary>Accepts TCP connections and serves HTTP/1.1 with keep-alive. Uses TcpListener only: no HttpListener, no Kestrel.</summary>
public sealed class Server(Router router, StaticFiles? files = null, int maxRequestsPerConnection = 100, TimeSpan? idleTimeout = null)
{
    private readonly TimeSpan _idle = idleTimeout ?? TimeSpan.FromSeconds(5);
    private TcpListener? _listener;

    /// <summary>Binds the socket and returns the actual port (pass 0 for an ephemeral port).</summary>
    public int Start(IPAddress address, int port)
    {
        _listener = new TcpListener(address, port);
        _listener.Start();
        return ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true) _ = HandleAsync(await _listener!.AcceptTcpClientAsync(ct), ct);
        }
        catch (OperationCanceledException) { }
        finally { _listener!.Stop(); }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                client.NoDelay = true;
                using var stream = new BufferedStream(client.GetStream());
                for (var served = 0; served < maxRequestsPerConnection; served++)
                {
                    HttpRequest? req;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(_idle);                         // idle keep-alive connections get closed
                    try { req = await HttpRequest.ReadAsync(stream, cts.Token); }
                    catch (HttpParseException e)
                    {
                        await HttpResponse.Text(400, e.Message).WriteAsync(stream, keepAlive: false, headOnly: false, ct);
                        return;
                    }
                    catch (OperationCanceledException) { return; }
                    if (req is null) return;                        // client closed the connection

                    var resp = Dispatch(req);
                    var keep = req.KeepAlive && served < maxRequestsPerConnection - 1;
                    await resp.WriteAsync(stream, keep, req.Method == "HEAD", ct);
                    Console.WriteLine($"{req.Method} {req.Path} -> {resp.Status}");
                    if (!keep) return;
                }
            }
        }
        catch (IOException) { /* peer reset the connection */ }
        catch (SocketException) { }
        catch (OperationCanceledException) { }
    }

    private HttpResponse Dispatch(HttpRequest req)
    {
        try
        {
            var m = router.Match(req.Method, req.Path);
            if (m.Kind == MatchKind.Matched) return m.Handler!(req, m.Args);
            if ((req.Method is "GET" or "HEAD") && files?.Serve(req.Path) is { } f) return f;
            return m.Kind == MatchKind.MethodNotAllowed ? HttpResponse.Text(405, "Method Not Allowed") : HttpResponse.Text(404, "Not Found");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return HttpResponse.Text(500, "Internal Server Error");
        }
    }
}
