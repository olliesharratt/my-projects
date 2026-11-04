using System.Text;

namespace TinyHttp;

public sealed class HttpParseException(string message) : Exception(message);

/// <summary>A parsed HTTP/1.x request. Parsing is done by hand from the raw byte stream.</summary>
public sealed record HttpRequest(string Method, string Path, string Query, string Version,
                                 Dictionary<string, string> Headers, byte[] Body)
{
    public const int MaxHeaderBytes = 16 * 1024;
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>HTTP/1.1 defaults to keep-alive, HTTP/1.0 to close; the Connection header overrides.</summary>
    public bool KeepAlive => Headers.TryGetValue("connection", out var c)
        ? !c.Equals("close", StringComparison.OrdinalIgnoreCase) && (Version == "HTTP/1.1" || c.Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
        : Version == "HTTP/1.1";

    /// <summary>Returns null if the peer closed the connection cleanly before sending anything.</summary>
    public static async Task<HttpRequest?> ReadAsync(Stream s, CancellationToken ct)
    {
        var head = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            if (await s.ReadAsync(one, ct) == 0)
            {
                if (head.Length == 0) return null;
                throw new HttpParseException("Unexpected end of request");
            }
            head.WriteByte(one[0]);
            if (head.Length > MaxHeaderBytes) throw new HttpParseException("Headers too large");
            if (EndsWithBlankLine(head)) break;
        }

        var text = Encoding.Latin1.GetString(head.GetBuffer(), 0, (int)head.Length);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var parts = lines[0].Split(' ');
        if (parts.Length != 3 || !parts[2].StartsWith("HTTP/1.")) throw new HttpParseException("Malformed request line");

        var target = parts[1];
        var q = target.IndexOf('?');
        var rawPath = q < 0 ? target : target[..q];
        var query = q < 0 ? "" : target[(q + 1)..];
        if (!rawPath.StartsWith('/')) throw new HttpParseException("Bad request target");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) throw new HttpParseException("Malformed header");
            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        if (headers.ContainsKey("Transfer-Encoding")) throw new HttpParseException("Chunked encoding not supported");

        var body = Array.Empty<byte>();
        if (headers.TryGetValue("Content-Length", out var cl))
        {
            if (!int.TryParse(cl, out var len) || len < 0 || len > MaxBodyBytes) throw new HttpParseException("Bad Content-Length");
            body = new byte[len];
            await s.ReadExactlyAsync(body, ct);
        }

        string path;
        try { path = Uri.UnescapeDataString(rawPath); } catch (UriFormatException) { throw new HttpParseException("Bad percent-encoding"); }
        return new HttpRequest(parts[0].ToUpperInvariant(), path, query, parts[2], headers, body);
    }

    private static bool EndsWithBlankLine(MemoryStream m)
    {
        var b = m.GetBuffer();
        var n = (int)m.Length;
        return n >= 4 && b[n - 4] == 13 && b[n - 3] == 10 && b[n - 2] == 13 && b[n - 1] == 10;
    }
}
