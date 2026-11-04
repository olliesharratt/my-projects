# Systems Projects

Three small projects that rebuild familiar tools to explore processes, concurrency, and network protocols.

| Project | Concepts | Stack |
|---|---|---|
| [mini-shell](mini-shell) | fork/exec, pipes, dup2, file descriptors, signals, process groups | C (POSIX) |
| [thread-pool](thread-pool) | mutexes, condition variables, bounded queues, back-pressure, benchmarking | C (pthreads) |
| [http-server](http-server) | TCP sockets, HTTP/1.1 parsing, keep-alive, routing, path-traversal defence | C# / .NET 8 |

## Run and Test

Run these commands from this directory. The C projects require a POSIX toolchain; the HTTP server requires the .NET 8 SDK.

| Project | Test command |
|---|---|
| Mini-shell | `make -C mini-shell test` |
| Thread pool | `make -C thread-pool test` |
| HTTP server | `dotnet test http-server/tests/TinyHttp.Tests` |

The thread-pool Makefile also provides `make -C thread-pool tsan` for a ThreadSanitizer run. Each project README contains its run instructions and design notes. This folder does not currently define a shared CI workflow.
