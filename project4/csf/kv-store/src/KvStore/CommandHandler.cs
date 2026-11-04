using System.Text.RegularExpressions;

namespace KvStore;

/// <summary>Line protocol (Redis-flavoured): +OK, -ERR msg, :integer, $value, $-1 for nil.</summary>
public static class CommandHandler
{
    public static string Execute(Store store, string line)
    {
        var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "-ERR empty command";
        switch (parts[0].ToUpperInvariant())
        {
            case "PING": return "+PONG";
            case "SET":
                if (parts.Length < 3) return "-ERR usage: SET key value [EX seconds]";
                var m = Regex.Match(parts[2], @"^(.*)\s+EX\s+(\d+)$");
                if (m.Success)
                {
                    if (!int.TryParse(m.Groups[2].Value, out var secs)) return "-ERR bad EX value";
                    store.Set(parts[1], m.Groups[1].Value, secs);
                }
                else store.Set(parts[1], parts[2]);
                return "+OK";
            case "GET":
                if (parts.Length < 2) return "-ERR usage: GET key";
                return store.Get(parts[1]) is { } v ? "$" + v : "$-1";
            case "DEL":
                if (parts.Length < 2) return "-ERR usage: DEL key";
                return ":" + (store.Del(parts[1]) ? 1 : 0);
            case "TTL":
                if (parts.Length < 2) return "-ERR usage: TTL key";
                return ":" + store.Ttl(parts[1]);
            case "DBSIZE": return ":" + store.Count;
            default: return "-ERR unknown command";
        }
    }
}
