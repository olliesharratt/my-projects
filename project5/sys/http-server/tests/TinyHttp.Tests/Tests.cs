using System.Net;
using System.Net.Sockets;
using System.Text;
using TinyHttp;
using Xunit;

public class ParserTests
{
    private static Task<HttpRequest?> Parse(string raw) =>
        HttpRequest.ReadAsync(new MemoryStream(Encoding.ASCII.GetBytes(raw)), CancellationToken.None);

    [Fact]
    public async Task ParsesRequestLineHeadersQueryAndBody()
    {
        var r = (await Parse("POST /a%20b?x=1 HTTP/1.1\r\nHost: t\r\nContent-Length: 5\r\n\r\nhello"))!;
        Assert.Equal("POST", r.Method);
        Assert.Equal("/a b", r.Path);
        Assert.Equal("x=1", r.Query);
        Assert.Equal("hello", Encoding.ASCII.GetString(r.Body));
        Assert.True(r.KeepAlive);
    }

    [Fact]
    public async Task ConnectionCloseAndHttp10DisableKeepAlive()
    {
        Assert.False((await Parse("GET / HTTP/1.1\r\nConnection: close\r\n\r\n"))!.KeepAlive);
        Assert.False((await Parse("GET / HTTP/1.0\r\n\r\n"))!.KeepAlive);
    }

    [Fact]
    public async Task MalformedRequestsThrow()
    {
        await Assert.ThrowsAsync<HttpParseException>(() => Parse("GARBAGE\r\n\r\n"));
        await Assert.ThrowsAsync<HttpParseException>(() => Parse("GET / HTTP/1.1\r\nNoColonHere\r\n\r\n"));
        await Assert.ThrowsAsync<HttpParseException>(() => Parse("GET / HTTP/1.1\r\nContent-Length: abc\r\n\r\n"));
    }

    [Fact]
    public async Task EmptyStreamMeansCleanClose() => Assert.Null(await Parse(""));
}

public class RouterTests
{
    private static readonly Handler Ok = (_, _) => HttpResponse.Text(200, "ok");

    [Fact]
    public void CapturesPathParameters()
    {
        var m = new Router().Add("GET", "/users/{id}/posts/{pid}", Ok).Match("GET", "/users/7/posts/9");
        Assert.Equal(MatchKind.Matched, m.Kind);
        Assert.Equal("7", m.Args["id"]);
        Assert.Equal("9", m.Args["pid"]);
    }

    [Fact]
    public void DistinguishesNotFoundFromMethodNotAllowed()
    {
        var r = new Router().Add("GET", "/a", Ok);
        Assert.Equal(MatchKind.NotFound, r.Match("GET", "/zzz").Kind);
        Assert.Equal(MatchKind.MethodNotAllowed, r.Match("POST", "/a").Kind);
        Assert.Equal(MatchKind.Matched, r.Match("HEAD", "/a").Kind);
    }
}

public class StaticFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    public StaticFileTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "www"));
        File.WriteAllText(Path.Combine(_dir, "www", "index.html"), "<h1>hi</h1>");
        File.WriteAllText(Path.Combine(_dir, "secret.txt"), "secret");
    }
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void ServesIndexForRoot() =>
        Assert.NotNull(new StaticFiles(Path.Combine(_dir, "www")).Resolve("/"));

    [Theory]
    [InlineData("/../secret.txt")]
    [InlineData("/..%2fsecret.txt")]
    [InlineData("/a/../../secret.txt")]
    public void BlocksPathTraversal(string url)
    {
        // The request parser percent-decodes before routing, so mimic that here.
        Assert.Null(new StaticFiles(Path.Combine(_dir, "www")).Resolve(Uri.UnescapeDataString(url)));
    }
}

public class ServerIntegrationTests
{
    private static async Task<(string Status, string Body)> ReadResponse(StreamReader r)
    {
        var status = await r.ReadLineAsync();
        var len = 0;
        string? line;
        while (!string.IsNullOrEmpty(line = await r.ReadLineAsync()))
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) len = int.Parse(line[15..].Trim());
        var buf = new char[len];
        for (var got = 0; got < len;) got += await r.ReadAsync(buf, got, len - got);
        return (status!, new string(buf));
    }

    [Fact]
    public async Task ServesMultipleRequestsOnOneConnection()
    {
        var router = new Router().Add("GET", "/hi/{name}", (_, a) => HttpResponse.Text(200, "hello " + a["name"]));
        var server = new Server(router);
        var port = server.Start(IPAddress.Loopback, 0);
        using var cts = new CancellationTokenSource();
        _ = server.AcceptLoopAsync(cts.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);

        foreach (var name in new[] { "ada", "linus" })      // same TCP connection both times = keep-alive works
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET /hi/{name} HTTP/1.1\r\nHost: x\r\n\r\n"));
            var (status, body) = await ReadResponse(reader);
            Assert.Equal("HTTP/1.1 200 OK", status);
            Assert.Equal("hello " + name, body);
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /nope HTTP/1.1\r\nHost: x\r\n\r\n"));
        Assert.Equal("HTTP/1.1 404 Not Found", (await ReadResponse(reader)).Status);
        cts.Cancel();
    }
}
