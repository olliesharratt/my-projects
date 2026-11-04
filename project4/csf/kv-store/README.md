# kv-store: a Redis-lite

In-memory key-value store with a line-based TCP protocol, key expiry and LRU eviction. C# / .NET 8, no dependencies.

```bash
dotnet run --project src/KvStore -- 6380 1000     # port, capacity
nc localhost 6380
SET name Ada
GET name            # $Ada
SET session abc EX 30
TTL session         # :30
DEL name            # :1
```

Commands: `PING`, `SET key value [EX seconds]`, `GET`, `DEL`, `TTL`, `DBSIZE`, `QUIT`.
Replies: `+OK`, `-ERR msg`, `:integer`, `$value`, `$-1` (nil).

## How it works
- **LRU:** `Dictionary` gives O(1) lookup; a hand-written doubly linked list with sentinel head/tail gives O(1) move-to-front and eviction. Every `GET` and `SET` is O(1).
- **Expiry:** lazy (checked on access) plus a once-per-second sweep, the same hybrid idea Redis uses. The clock is injected so tests don't sleep.
- **Concurrency:** one lock around the store. A read mutates recency order, so even reads need exclusive access. A good discussion point: sharding by key hash would reduce contention.
- **Testable core:** `CommandHandler.Execute` is a pure function of (store, line), so the protocol is tested without sockets.

Next steps: pub/sub, `INCR`, persistence (append-only log), RESP protocol compatibility so `redis-cli` works, benchmarks.
