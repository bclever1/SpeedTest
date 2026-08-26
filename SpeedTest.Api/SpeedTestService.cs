using System.Diagnostics;
using System.Text.Json;

namespace SpeedTest.Api;

public class SpeedTestService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SpeedTestService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);

    public SpeedTestService(IServiceProvider services, ILogger<SpeedTestService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run first test after a short delay
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting speed test...");
                var result = await RunSpeedTest(stoppingToken);
                if (result != null)
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.SpeedTestResults.Add(result);
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Speed test complete: {Down:.##} Mbps down, {Up:.##} Mbps up, {Ping:.#} ms ping",
                        result.DownloadMbps, result.UploadMbps, result.PingMs);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Speed test failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    public static async Task<SpeedTestResult?> RunSpeedTest(CancellationToken ct = default)
    {
        var speedtestPath = FindSpeedtest();
        if (speedtestPath == null) return null;

        var psi = new ProcessStartInfo
        {
            FileName = speedtestPath,
            Arguments = "--accept-license --accept-gdpr -f json",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return null;

        await proc.StandardInput.WriteLineAsync("Y");
        proc.StandardInput.Close();

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0) return null;

        // Extract JSON from output (may contain license text before it)
        var jsonStart = output.IndexOf('{');
        if (jsonStart < 0) return null;
        output = output[jsonStart..];

        var json = JsonDocument.Parse(output);
        var root = json.RootElement;

        var downloadBw = root.GetProperty("download").GetProperty("bandwidth").GetDouble();
        var uploadBw = root.GetProperty("upload").GetProperty("bandwidth").GetDouble();
        var ping = root.GetProperty("ping").GetProperty("latency").GetDouble();
        var jitter = root.GetProperty("ping").GetProperty("jitter").GetDouble();
        var packetLoss = root.TryGetProperty("packetLoss", out var pl) ? pl.GetDouble() : 0;
        var isp = root.GetProperty("isp").GetString() ?? "";
        var server = root.GetProperty("server");
        var resultUrl = root.GetProperty("result").GetProperty("url").GetString() ?? "";

        return new SpeedTestResult
        {
            MachineName = Environment.MachineName,
            Timestamp = DateTime.UtcNow,
            DownloadMbps = Math.Round(downloadBw * 8 / 1_000_000, 2),
            UploadMbps = Math.Round(uploadBw * 8 / 1_000_000, 2),
            PingMs = Math.Round(ping, 1),
            Jitter = Math.Round(jitter, 1),
            PacketLoss = Math.Round(packetLoss, 2),
            Isp = isp,
            ServerName = server.GetProperty("name").GetString() ?? "",
            ServerLocation = server.GetProperty("location").GetString() ?? "",
            ResultUrl = resultUrl
        };
    }

    private static string? FindSpeedtest()
    {
        var candidates = new[]
        {
            @"C:\ProgramData\chocolatey\bin\speedtest.exe",
            "/usr/bin/speedtest",
            "/usr/local/bin/speedtest"
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
