using System.Text.Json;

namespace RaftLite;

/// <summary>Wire format: one JSON datagram {from, type, body}.</summary>
public static class Codec
{
    private sealed record Envelope(string From, string Type, JsonElement Body);

    public static string Encode(string from, IMessage m) =>
        JsonSerializer.Serialize(new Envelope(from, m.GetType().Name, JsonSerializer.SerializeToElement(m, m.GetType())));

    /// <summary>Returns null for anything malformed or unknown; a network-facing parser must never throw on bad input.</summary>
    public static (string From, IMessage Message)? Decode(string json)
    {
        try
        {
            var e = JsonSerializer.Deserialize<Envelope>(json);
            if (e is null || string.IsNullOrEmpty(e.From)) return null;
            IMessage? m = e.Type switch
            {
                nameof(RequestVote) => e.Body.Deserialize<RequestVote>(),
                nameof(VoteReply) => e.Body.Deserialize<VoteReply>(),
                nameof(AppendEntries) => e.Body.Deserialize<AppendEntries>(),
                nameof(AppendReply) => e.Body.Deserialize<AppendReply>(),
                _ => null
            };
            return m is null ? null : (e.From, m);
        }
        catch (JsonException) { return null; }
    }
}
