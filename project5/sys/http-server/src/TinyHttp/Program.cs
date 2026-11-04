using System.Net;
using System.Text;
using TinyHttp;

var port = args.Length > 0 ? int.Parse(args[0]) : 8080;
var root = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "wwwroot");

var router = new Router()
    .Add("GET", "/api/hello", (r, a) => HttpResponse.Json(new { message = "Hello from raw sockets" }))
    .Add("GET", "/api/time", (r, a) => HttpResponse.Json(new { utc = DateTime.UtcNow }))
    .Add("GET", "/api/users/{id}", (r, a) => HttpResponse.Json(new { id = a["id"], name = $"user-{a["id"]}" }))
    .Add("POST", "/api/echo", (r, a) => HttpResponse.Text(200, Encoding.UTF8.GetString(r.Body)));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var server = new Server(router, new StaticFiles(root));
var bound = server.Start(IPAddress.Any, port);
Console.WriteLine($"TinyHttp on http://localhost:{bound}  (static files from {root}; Ctrl+C to stop)");
await server.AcceptLoopAsync(cts.Token);
