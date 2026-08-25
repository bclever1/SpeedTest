namespace SpeedTest.Api;

public class SpeedTestResult
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
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
