using RateLimiting;

var app = WebApplication.CreateBuilder(args).Build();

// Burst of 5, then 1 request per second per client IP.
app.UseRateLimiting(new TokenBucketLimiter(capacity: 5, refillPerSecond: 1));

app.MapGet("/", () => "hello");
app.Run();
