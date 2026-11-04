# Chat server: SignalR rooms, history and reconnect catch-up

ASP.NET Core 8 + SignalR backend with a dependency-light browser client.

```bash
dotnet run --project src/ChatServer        # open http://localhost:5000 in two tabs
dotnet test tests/ChatServer.Tests
```

## Features
- **Rooms** (`JoinRoom` / `LeaveRoom` / `SendMessage`) built on SignalR groups, with validated room names and message length.
- **Presence:** each room broadcasts its user list on joins, leaves and disconnects. Tracking by connection id means two tabs for one user count once.
- **History:** the last 100 messages per room, each with a per-room sequence number.
- **Reconnect handling (the interesting part):** after a network drop SignalR gives the client a *new connection id* and the server forgets its group membership. The client therefore rejoins every room, passing the last sequence number it saw; the server replays exactly the missed messages. Duplicates are discarded by sequence number. If automatic retries are exhausted the UI shows a Reconnect button.
- **Ordering guarantee:** "assign sequence number and broadcast" runs under a per-room semaphore, so clients never see message 5 before 4 (which would break the "last seen" bookmark).
- Authorisation check: you can't send to a room you haven't joined.
- The client renders with `textContent`, so messages can't inject HTML.

## Tests
Unit tests for sequencing, bounded history, concurrency and presence. Integration tests run the real hub in memory (long polling, since `TestServer` has no WebSockets) and prove broadcast, catch-up after reconnect, and the join-before-send rule.

## Limits (and what I'd do next)
State lives in memory, so it is single-instance and lost on restart. To scale out: a Redis backplane (`AddStackExchangeRedis`) for SignalR, and a database for history. Also: real authentication instead of a `?user=` query string (anyone can claim any name), rate limiting per connection (see the rate-limiter project), typing indicators, and private messages.
