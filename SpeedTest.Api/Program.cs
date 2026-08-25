using System.Diagnostics;

var Version = "1.0.0";

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100_000_000; // 100MB for upload tests
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// --- Ping / Latency ---
app.MapGet("/api/ping", () => Results.Ok(new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));

// --- Download Speed Test ---
// Returns a chunk of random data. Client measures how long it takes to receive.
app.MapGet("/api/download", (int? size) =>
{
    // Default 10MB, max 100MB
    var bytes = Math.Min(size ?? 10_000_000, 100_000_000);
    var data = new byte[bytes];
    Random.Shared.NextBytes(data);

    return Results.File(data, "application/octet-stream");
});

// --- Upload Speed Test ---
// Client sends a blob of data, server measures receive time.
app.MapPost("/api/upload", async (HttpRequest request) =>
{
    var sw = Stopwatch.StartNew();
    long totalBytes = 0;
    var buffer = new byte[65536];

    await using var body = request.Body;
    int read;
    while ((read = await body.ReadAsync(buffer)) > 0)
    {
        totalBytes += read;
    }

    sw.Stop();
    var seconds = sw.Elapsed.TotalSeconds;
    var mbps = (totalBytes * 8.0 / 1_000_000) / seconds;

    return Results.Ok(new
    {
        bytesReceived = totalBytes,
        durationMs = sw.Elapsed.TotalMilliseconds,
        mbps = Math.Round(mbps, 2)
    });
});

// --- Version ---
app.MapGet("/api/version", () => Results.Ok(new { version = Version }));

app.Run();
