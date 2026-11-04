using System.Text;
using System.Text.Json;

namespace TinyHttp;

public sealed class HttpResponse
{
    public int Status { get; init; } = 200;
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body { get; init; } = [];

    public static HttpResponse Text(int status, string text, string contentType = "text/plain; charset=utf-8") =>
        new() { Status = status, Body = Encoding.UTF8.GetBytes(text), Headers = { ["Content-Type"] = contentType } };

    public static HttpResponse Json(object value, int status = 200) =>
        Text(status, JsonSerializer.Serialize(value), "application/json; charset=utf-8");

    private static string Reason(int status) => status switch
    {
        200 => "OK", 201 => "Created", 204 => "No Content", 400 => "Bad Request", 403 => "Forbidden",
        404 => "Not Found", 405 => "Method Not Allowed", 500 => "Internal Server Error", _ => "Unknown"
    };

    /// <summary>Serialises status line, headers and body. HEAD responses carry headers (including the real Content-Length) but no body.</summary>
    public async Task WriteAsync(Stream s, bool keepAlive, bool headOnly, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {Status} {Reason(Status)}\r\n");
        sb.Append("Server: TinyHttp\r\n");
        sb.Append($"Date: {DateTime.UtcNow:R}\r\n");
        foreach (var (k, v) in Headers) sb.Append($"{k}: {v}\r\n");
        sb.Append($"Content-Length: {Body.Length}\r\n");
        sb.Append(keepAlive ? "Connection: keep-alive\r\n" : "Connection: close\r\n");
        sb.Append("\r\n");
        await s.WriteAsync(Encoding.ASCII.GetBytes(sb.ToString()), ct);
        if (!headOnly) await s.WriteAsync(Body, ct);
        await s.FlushAsync(ct);
    }
}
