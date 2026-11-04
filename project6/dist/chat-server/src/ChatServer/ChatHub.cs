using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace ChatServer;

public sealed partial class ChatHub(RoomStore store, PresenceTracker presence) : Hub
{
    private const int MaxTextLength = 2000;

    [GeneratedRegex("^[a-z0-9_-]{1,32}$")]
    private static partial Regex RoomName();

    private string UserName
    {
        get
        {
            var u = Context.GetHttpContext()?.Request.Query["user"].ToString().Trim() ?? "";
            return u.Length == 0 ? $"guest-{Context.ConnectionId[..4]}" : u[..Math.Min(u.Length, 32)];
        }
    }

    private static string Normalize(string room)
    {
        room = (room ?? "").Trim().ToLowerInvariant();
        if (!RoomName().IsMatch(room)) throw new HubException("Room names are 1-32 characters: letters, digits, - and _.");
        return room;
    }

    /// <summary>
    /// Joins a room and returns everything after <paramref name="lastSeenSeq"/>. A fresh join passes 0; a client that just
    /// reconnected passes the last sequence number it saw, so it gets exactly the messages it missed.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> JoinRoom(string room, long lastSeenSeq)
    {
        room = Normalize(room);
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        presence.Join(room, Context.ConnectionId, UserName);
        await Clients.Group(room).SendAsync("Presence", room, presence.Users(room));
        return store.Since(room, lastSeenSeq);
    }

    public async Task LeaveRoom(string room)
    {
        room = Normalize(room);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
        if (presence.Leave(room, Context.ConnectionId))
            await Clients.Group(room).SendAsync("Presence", room, presence.Users(room));
    }

    public async Task SendMessage(string room, string text)
    {
        room = Normalize(room);
        text = (text ?? "").Trim();
        if (text.Length == 0) throw new HubException("Message is empty.");
        if (text.Length > MaxTextLength) throw new HubException($"Message is longer than {MaxTextLength} characters.");
        if (!presence.IsIn(room, Context.ConnectionId)) throw new HubException("Join the room before sending.");
        await store.PostAsync(room, UserName, text, m => Clients.Group(room).SendAsync("Message", m));
    }

    // SignalR removes the connection from its groups itself; we only need to update presence.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var room in presence.LeaveAll(Context.ConnectionId))
            await Clients.Group(room).SendAsync("Presence", room, presence.Users(room));
        await base.OnDisconnectedAsync(exception);
    }
}
