# CS Fundamentals

Three small projects that implement core data structures and algorithms, with a focus on understanding the trade-offs behind each design.

| Project | Concepts | Stack |
|---|---|---|
| [pathfinding-visualiser](pathfinding-visualiser) | BFS, Dijkstra, A*, binary heap | HTML canvas, JavaScript |
| [kv-store](kv-store) | Hash map + doubly linked list (LRU), TTL expiry, TCP protocol, locking | C# / .NET 8 |
| [search-engine](search-engine) | Inverted index, BM25, Levenshtein distance | C# / .NET 8 |

## Run and Test

Run these commands from this directory. The .NET projects require the .NET 8 SDK.

| Project | Run | Test |
|---|---|---|
| Pathfinding visualiser | Open [index.html](pathfinding-visualiser/index.html) in a browser | Manual interaction; no automated test suite |
| Key-value store | `dotnet run --project kv-store/src/KvStore -- 6380 1000` | `dotnet test kv-store/tests/KvStore.Tests` |
| Search engine | `dotnet run --project search-engine/src/SearchEngine -- search-engine/sample-docs` | `dotnet test search-engine/tests/SearchEngine.Tests` |

Each project README describes its implementation, example usage, and current limitations. This folder does not currently define a shared CI workflow.
