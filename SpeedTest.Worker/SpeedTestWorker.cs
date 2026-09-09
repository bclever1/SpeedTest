using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SpeedTest.Worker;

public class SpeedTestWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SpeedTestWorker> _logger;
    private readonly IConfiguration _config;

    public SpeedTestWorker(IHttpClientFactory httpFactory, ILogger<SpeedTestWorker> logger, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(_config.GetValue("IntervalMinutes", 30));
        var apiUrl = _config["CentralApiUrl"] ?? "http://192.168.50.200:5091";

        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Running speed test...");
                var result = await RunSpeedTest(ct);
                if (result != null)
                {
                    var client = _httpFactory.CreateClient();
                    var json = JsonSerializer.Serialize(result);
                    var response = await client.PostAsync(
                        $"{apiUrl}/api/results/submit",
                        new StringContent(json, Encoding.UTF8, "application/json"),
                        ct);

                    if (response.IsSuccessStatusCode)
                        _logger.LogInformation("Result submitted: {Down:.##} Mbps down, {Up:.##} Mbps up, {Ping:.#} ms ping",
                            result.DownloadMbps, result.UploadMbps, result.PingMs);
                    else
                        _logger.LogWarning("Failed to submit result: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Speed test cycle failed");
            }

            await Task.Delay(interval, ct);
        }
    }

    private async Task<SpeedTestDto?> RunSpeedTest(CancellationToken ct)
    {
        var speedtestPath = FindSpeedtest();
        if (speedtestPath == null)
        {
            _logger.LogError("speedtest CLI not found");
            return null;
        }

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

        // Send "Y" to accept license if prompted
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

        return new SpeedTestDto
        {
            MachineName = Environment.MachineName,
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WinGet\Links\speedtest.exe"),
            @"C:\Program Files\Ookla\Speedtest\speedtest.exe",
            @"C:\Program Files (x86)\Ookla\Speedtest\speedtest.exe",
            "/usr/bin/speedtest",
            "/usr/local/bin/speedtest"
        };
        // Check known paths first, then fall back to PATH
        return candidates.FirstOrDefault(File.Exists)
            ?? FindInPath("speedtest.exe")
            ?? FindInPath("speedtest");
    }

    private static string? FindInPath(string filename)
    {
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return pathDirs.Select(dir => Path.Combine(dir, filename)).FirstOrDefault(File.Exists);
    }
}

public class SpeedTestDto
{
    public string MachineName { get; set; } = "";
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public double Jitter { get; set; }
    public double PacketLoss { get; set; }
    public string Isp { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerLocation { get; set; } = "";
    public string ResultUrl { get; set; } = "";
}
