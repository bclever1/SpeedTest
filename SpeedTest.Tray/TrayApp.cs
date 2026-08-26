using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json;

namespace SpeedTest.Tray;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly SpeedOverlay _overlay;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HttpClient _http = new();
    private readonly string _apiUrl;
    private readonly string _machineName = Environment.MachineName;

    public TrayApp()
    {
        _apiUrl = File.Exists("appsettings.json")
            ? JsonDocument.Parse(File.ReadAllText("appsettings.json"))
                .RootElement.GetProperty("CentralApiUrl").GetString() ?? "http://192.168.1.200:5091"
            : "http://192.168.1.200:5091";

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "SpeedTest",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _overlay = new SpeedOverlay();
        _overlay.Show();

        _timer = new System.Windows.Forms.Timer { Interval = 5_000 };
        _timer.Tick += async (_, _) => await PollLatest();
        _timer.Start();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Dashboard", null, (_, _) =>
            Process.Start(new ProcessStartInfo(_apiUrl) { UseShellExecute = true }));
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _tray.Visible = false;
            _overlay.Close();
            Application.Exit();
        });
        return menu;
    }

    private async Task PollLatest()
    {
        _timer.Interval = 60_000;

        try
        {
            var response = await _http.GetStringAsync(
                $"{_apiUrl}/api/results?days=1&machine={_machineName}");
            var results = JsonDocument.Parse(response).RootElement;

            if (results.GetArrayLength() == 0)
            {
                _overlay.Update("--", "--", "--", false);
                _tray.Text = $"SpeedTest ({_machineName}) — no results yet";
                return;
            }

            var latest = results[0];
            var down = latest.GetProperty("downloadMbps").GetDouble();
            var up = latest.GetProperty("uploadMbps").GetDouble();
            var ping = latest.GetProperty("pingMs").GetDouble();
            var ts = DateTime.Parse(latest.GetProperty("timestamp").GetString()!).ToLocalTime();

            _overlay.Update(
                $"{down:0.#}",
                $"{up:0.#}",
                $"{ping:0}",
                down >= 100);

            var text = $"↓ {down} Mbps  ↑ {up} Mbps\n" +
                       $"Ping: {ping} ms\n" +
                       $"{_machineName} @ {ts:h:mm tt}";
            _tray.Text = text.Length > 127 ? text[..127] : text;
        }
        catch
        {
            _overlay.Update("!", "--", "--", false);
            _tray.Text = "SpeedTest — can't reach API";
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _overlay.Dispose();
            _timer.Dispose();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class SpeedOverlay : Form
{
    private string _down = "--";
    private string _up = "--";
    private string _ping = "--";
    private bool _good;

    public SpeedOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(140, 40);
        Opacity = 0.9;

        PositionOnTaskbar();

        // Allow dragging
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { Capture = false; Message m = Message.Create(Handle, 0xA1, 2, 0); WndProc(ref m); } };
    }

    private void PositionOnTaskbar()
    {
        var screen = Screen.PrimaryScreen!.WorkingArea;
        // Bottom-left, just above taskbar, right of weather widget area
        Left = 160;
        Top = screen.Bottom - Height - 4;
    }

    public void Update(string down, string up, string ping, bool good)
    {
        _down = down;
        _up = up;
        _ping = ping;
        _good = good;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Background with rounded corners
        using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        using var path = RoundedRect(new Rectangle(0, 0, Width, Height), 8);
        g.FillPath(bgBrush, path);

        // Download speed — large
        var downColor = _good ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);
        using var bigFont = new Font("Segoe UI", 14f, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var downBrush = new SolidBrush(downColor);
        using var dimBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

        // "↓ 56.2" on the left
        g.DrawString($"↓{_down}", bigFont, downBrush, 4, 4);

        // "↑ 5.9" and ping on the right side, stacked
        var rightX = 85f;
        g.DrawString($"↑{_up}", smallFont, dimBrush, rightX, 4);
        g.DrawString($"{_ping}ms", smallFont, dimBrush, rightX, 20);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW — hide from Alt+Tab
            return cp;
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
