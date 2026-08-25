using Microsoft.EntityFrameworkCore;
using SpeedTest.Api;

var Version = "2.0.0";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=speedtest.db"));

builder.Services.AddHostedService<SpeedTestService>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// --- Get all results (newest first) ---
app.MapGet("/api/results", async (AppDbContext db, int? days) =>
{
    var since = DateTime.UtcNow.AddDays(-(days ?? 7));
    return await db.SpeedTestResults
        .Where(r => r.Timestamp >= since)
        .OrderByDescending(r => r.Timestamp)
        .ToListAsync();
});

// --- Get summary stats ---
app.MapGet("/api/stats", async (AppDbContext db, int? days) =>
{
    var since = DateTime.UtcNow.AddDays(-(days ?? 7));
    var results = await db.SpeedTestResults
        .Where(r => r.Timestamp >= since)
        .ToListAsync();

    if (results.Count == 0)
        return Results.Ok(new { count = 0 });

    return Results.Ok(new
    {
        count = results.Count,
        download = new
        {
            avg = Math.Round(results.Average(r => r.DownloadMbps), 2),
            min = Math.Round(results.Min(r => r.DownloadMbps), 2),
            max = Math.Round(results.Max(r => r.DownloadMbps), 2),
            median = Math.Round(Median(results.Select(r => r.DownloadMbps)), 2)
        },
        upload = new
        {
            avg = Math.Round(results.Average(r => r.UploadMbps), 2),
            min = Math.Round(results.Min(r => r.UploadMbps), 2),
            max = Math.Round(results.Max(r => r.UploadMbps), 2),
            median = Math.Round(Median(results.Select(r => r.UploadMbps)), 2)
        },
        ping = new
        {
            avg = Math.Round(results.Average(r => r.PingMs), 1),
            min = Math.Round(results.Min(r => r.PingMs), 1),
            max = Math.Round(results.Max(r => r.PingMs), 1)
        },
        isp = results.Last().Isp
    });
});

// --- Run a test manually ---
app.MapPost("/api/test", async (AppDbContext db) =>
{
    var result = await SpeedTestService.RunSpeedTest();
    if (result == null)
        return Results.Problem("Speed test failed — is speedtest CLI installed?");

    db.SpeedTestResults.Add(result);
    await db.SaveChangesAsync();
    return Results.Ok(result);
});

// --- Version ---
app.MapGet("/api/version", () => Results.Ok(new { version = Version }));

app.Run();

static double Median(IEnumerable<double> values)
{
    var sorted = values.OrderBy(v => v).ToList();
    int n = sorted.Count;
    if (n == 0) return 0;
    return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
}
