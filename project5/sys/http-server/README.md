# TinyHttp: an HTTP/1.1 server on raw sockets

C# / .NET 8. Uses `TcpListener` and streams only: no `HttpListener`, no Kestrel, no ASP.NET.

```bash
dotnet run --project src/TinyHttp -- 8080
curl -i localhost:8080/api/users/42
curl -i -X POST -d 'hello' localhost:8080/api/echo
curl -i localhost:8080/            # static file from wwwroot
dotnet test tests/TinyHttp.Tests
```

## What it does
- **Request parsing by hand:** reads bytes until `\r\n\r\n`, parses the request line and headers, then reads exactly `Content-Length` bytes. Limits: 16 KB of headers, 1 MB body. Bad input gets a `400`.
- **Routing:** method + pattern (`/users/{id}`), with `404` vs `405` distinguished; `HEAD` is served by `GET` routes.
- **Static files** with MIME types and **path-traversal protection** (resolved full path must stay under the root; `..` and `%2e%2e` are rejected).
- **Keep-alive:** HTTP/1.1 connections are reused for up to 100 requests and closed after 5 s idle; `Connection: close` and HTTP/1.0 are honoured.
- Handler exceptions become `500` without crashing the server.

## What ASP.NET does for you (the interview talking points)
| You wrote by hand here | ASP.NET / Kestrel gives you |
|---|---|
| Header parsing, size limits | Hardened parser, HTTP/2, HTTP/3 |
| `Router` | Endpoint routing, constraints, model binding |
| Read/write on `BufferedStream` | Pipelines with pooled buffers and zero-copy parsing |
| Per-connection `Task` | Tuned scheduling, connection limits, graceful shutdown |
| `Connection` header logic | Keep-alive, pipelining and timeouts out of the box |
| (not implemented) | TLS, chunked encoding, compression, auth, middleware |

## Next steps
Chunked transfer encoding, `If-Modified-Since`/ETag caching, a middleware pipeline, TLS via `SslStream`, `System.IO.Pipelines` for parsing, a load test with `wrk` to find the bottleneck.
