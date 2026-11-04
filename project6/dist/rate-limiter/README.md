# RateLimiting: token bucket and sliding window for .NET

A small library with two algorithms behind one interface (`IRateLimiter`) and an ASP.NET Core middleware.

```csharp
var limiter = new TokenBucketLimiter(capacity: 5, refillPerSecond: 1);   // or: new SlidingWindowLimiter(100, TimeSpan.FromMinutes(1))
app.UseRateLimiting(limiter);                                             // keyed by client IP by default
app.UseRateLimiting(limiter, ctx => ctx.Request.Headers["X-Api-Key"].ToString());   // or by API key
```
```bash
dotnet run --project samples/Demo
for i in $(seq 8); do curl -s -o /dev/null -w "%{http_code} " localhost:5000/; done   # 200 x5, then 429
dotnet test tests/RateLimiting.Tests
```
Responses carry `X-RateLimit-Limit`, `X-RateLimit-Remaining` and, on rejection, `Retry-After`.

## Algorithm comparison
| | Token bucket | Sliding window counter | Fixed window (not included) |
|---|---|---|---|
| Memory per key | O(1) | O(1) | O(1) |
| Bursts | Allowed up to capacity, then steady rate | Smoothed | Up to 2x limit across a window boundary |
| Accuracy | Exact | Approximate (assumes even spread) | Exact within a window |
| Best for | APIs that tolerate short bursts | Strict "N per minute" limits | Simplicity |

(An exact *sliding window log* stores every timestamp: precise, but O(limit) memory per key.)

## Design notes
- **Lazy refill:** tokens earned are computed from elapsed time on each call, so there is no timer per bucket.
- **Concurrency:** `ConcurrentDictionary` finds the per-key state; a lock on that one bucket protects its read-modify-write. Different keys never contend. A test hammers one key from 1000 parallel calls and asserts exactly `capacity` succeed.
- **Testable time:** limiters take a `TimeProvider`, so tests advance a fake clock instead of sleeping.
- **Memory growth:** `Sweep(idle)` evicts idle keys (safe, since an idle bucket is full anyway). Run it from a `BackgroundService` timer.

## Limits of this design (and what I'd do next)
It is single-process: behind several instances each has its own counters. The distributed fix is a shared store (Redis with a Lua script for atomic token-bucket updates). Also: a hosted-service cleanup, `IOptions` configuration, per-route policies, and a benchmark (BenchmarkDotNet) comparing the two algorithms.
