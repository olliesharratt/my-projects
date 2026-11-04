using ChatServer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

public class RoomStoreTests
{
    private static Task Noop(ChatMessage _) => Task.CompletedTask;

    [Fact]
    public async Task AssignsIncreasingSequenceNumbersPerRoom()
    {
        var s = new RoomStore();
        var a = await s.PostAsync("r1", "u", "one", Noop);
        var b = await s.PostAsync("r1", "u", "two", Noop);
        var c = await s.PostAsync("r2", "u", "other room", Noop);
        Assert.Equal([1L, 2L, 1L], new[] { a.Seq, b.Seq, c.Seq });
    }

    [Fact]
    public async Task SinceReturnsOnlyNewerMessages()
    {
        var s = new RoomStore();
        for (var i = 1; i <= 5; i++) await s.PostAsync("r", "u", $"m{i}", Noop);
        Assert.Equal(["m4", "m5"], s.Since("r", 3).Select(m => m.Text));
        Assert.Empty(s.Since("r", 5));
        Assert.Empty(s.Since("missing", 0));
    }

    [Fact]
    public async Task HistoryIsBounded()
    {
        var s = new RoomStore(maxHistory: 3);
        for (var i = 1; i <= 10; i++) await s.PostAsync("r", "u", $"m{i}", Noop);
        Assert.Equal([8L, 9L, 10L], s.Since("r", 0).Select(m => m.Seq));
    }

    [Fact]
    public async Task ConcurrentPostsGetUniqueSequenceNumbers()
    {
        var s = new RoomStore(maxHistory: 1000);
        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => s.PostAsync("r", "u", $"m{i}", Noop)));
        Assert.Equal(200, s.Since("r", 0).Select(m => m.Seq).Distinct().Count());
    }
}

public class PresenceTests
{
    [Fact]
    public void SameUserInTwoTabsAppearsOnceAndLeavesWhenBothClose()
    {
        var p = new PresenceTracker();
        p.Join("r", "conn1", "ada");
        p.Join("r", "conn2", "ada");
        Assert.Equal(["ada"], p.Users("r"));
        p.LeaveAll("conn1");
        Assert.Equal(["ada"], p.Users("r"));
        Assert.Equal(["r"], p.LeaveAll("conn2"));
        Assert.Empty(p.Users("r"));
    }
}

public class ChatIntegrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HubConnection Connect(string user) => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, $"chat?user={user}"), o =>
        {
            o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();   // in-memory server
            o.Transports = HttpTransportType.LongPolling;                         // TestServer has no real WebSockets
        })
        .Build();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task MessageIsBroadcastToEveryoneInTheRoom()
    {
        var ada = Connect("ada"); var bob = Connect("bob");
        var got = new TaskCompletionSource<ChatMessage>();
        bob.On<ChatMessage>("Message", m => got.TrySetResult(m));
        await ada.StartAsync(); await bob.StartAsync();
        await ada.InvokeAsync<List<ChatMessage>>("JoinRoom", "broadcast", 0L);
        await bob.InvokeAsync<List<ChatMessage>>("JoinRoom", "broadcast", 0L);

        await ada.InvokeAsync("SendMessage", "broadcast", "hello bob");

        var m = await got.Task.WaitAsync(Timeout);
        Assert.Equal(("ada", "hello bob"), (m.User, m.Text));
        await ada.DisposeAsync(); await bob.DisposeAsync();
    }

    [Fact]
    public async Task ReconnectingClientReceivesExactlyTheMessagesItMissed()
    {
        var ada = Connect("ada");
        await ada.StartAsync();
        await ada.InvokeAsync<List<ChatMessage>>("JoinRoom", "resume", 0L);
        await ada.InvokeAsync("SendMessage", "resume", "one");

        var bob = Connect("bob");
        await bob.StartAsync();
        var history = await bob.InvokeAsync<List<ChatMessage>>("JoinRoom", "resume", 0L);
        var lastSeen = history.Max(m => m.Seq);
        Assert.Equal(["one"], history.Select(m => m.Text));

        await bob.StopAsync();                                   // bob drops off...
        await ada.InvokeAsync("SendMessage", "resume", "two");
        await ada.InvokeAsync("SendMessage", "resume", "three");

        var bob2 = Connect("bob");                               // ...and comes back with a new connection id
        await bob2.StartAsync();
        var missed = await bob2.InvokeAsync<List<ChatMessage>>("JoinRoom", "resume", lastSeen);
        Assert.Equal(["two", "three"], missed.Select(m => m.Text));
        await ada.DisposeAsync(); await bob2.DisposeAsync();
    }

    [Fact]
    public async Task CannotSendToARoomYouHaveNotJoined()
    {
        var c = Connect("eve");
        await c.StartAsync();
        await Assert.ThrowsAsync<HubException>(() => c.InvokeAsync("SendMessage", "locked", "hi"));
        await c.DisposeAsync();
    }
}
