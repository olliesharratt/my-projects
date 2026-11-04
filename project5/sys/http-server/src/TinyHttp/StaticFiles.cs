namespace TinyHttp;

public sealed class StaticFiles(string root)
{
    private readonly string _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    /// <summary>Maps a URL path to a file under the root. Anything that escapes the root (../, %2e%2e/) yields null.</summary>
    public string? Resolve(string urlPath)
    {
        try
        {
            var rel = urlPath.TrimStart('/');
            if (rel.Length == 0 || rel.EndsWith('/')) rel += "index.html";
            var full = Path.GetFullPath(Path.Combine(_root, rel));
            return full.StartsWith(_root, StringComparison.Ordinal) && File.Exists(full) ? full : null;
        }
        catch (ArgumentException) { return null; }
    }

    public HttpResponse? Serve(string urlPath)
    {
        var file = Resolve(urlPath);
        if (file is null) return null;
        return new HttpResponse { Body = File.ReadAllBytes(file), Headers = { ["Content-Type"] = Mime(file) } };
    }

    private static string Mime(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".svg" => "image/svg+xml",
        ".txt" => "text/plain; charset=utf-8",
        _ => "application/octet-stream"
    };
}
