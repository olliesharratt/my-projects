namespace TinyHttp;

public delegate HttpResponse Handler(HttpRequest request, IReadOnlyDictionary<string, string> args);
public enum MatchKind { NotFound, MethodNotAllowed, Matched }
public sealed record RouteMatch(MatchKind Kind, Handler? Handler, Dictionary<string, string> Args);

/// <summary>Method + path-pattern routing. Patterns like /users/{id} capture segments as named args.</summary>
public sealed class Router
{
    private readonly List<(string Method, string[] Segments, Handler Handler)> _routes = [];

    public Router Add(string method, string pattern, Handler handler)
    {
        _routes.Add((method.ToUpperInvariant(), Split(pattern), handler));
        return this;
    }

    public RouteMatch Match(string method, string path)
    {
        var segs = Split(path);
        var pathMatched = false;
        foreach (var (m, pattern, handler) in _routes)
        {
            if (pattern.Length != segs.Length) continue;
            var args = new Dictionary<string, string>();
            var ok = true;
            for (var i = 0; i < pattern.Length && ok; i++)
            {
                if (pattern[i].StartsWith('{') && pattern[i].EndsWith('}')) args[pattern[i][1..^1]] = segs[i];
                else ok = pattern[i] == segs[i];
            }
            if (!ok) continue;
            pathMatched = true;
            if (m == method || (method == "HEAD" && m == "GET")) return new(MatchKind.Matched, handler, args);
        }
        return new(pathMatched ? MatchKind.MethodNotAllowed : MatchKind.NotFound, null, []);
    }

    private static string[] Split(string p) => p.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
