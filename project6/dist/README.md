# Distributed Systems and Networking

| Project | Concepts | Stack |
|---|---|---|
| [rate-limiter](rate-limiter) | Token bucket, sliding window counter, per-key locking, ASP.NET Core middleware | C# / .NET 8 |
| [chat-server](chat-server) | WebSockets/SignalR, rooms, presence, message sequencing, reconnect catch-up | C# / SignalR + vanilla JS |
| [raft-lite](raft-lite) | Consensus, leader election, terms, quorums, failure testing with a deterministic simulator | C# / .NET 8 |

Each folder has its own README with design notes and limits. CI runs all tests.
