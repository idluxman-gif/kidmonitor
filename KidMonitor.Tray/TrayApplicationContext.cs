using System.Diagnostics;
using System.Reflection;

namespace KidMonitor.Tray;

/// <summary>
/// WinForms ApplicationContext that provides the system tray icon for KidMonitor.
/// Polls the local dashboard API to display service health status.
/// Opens the browser to the dashboard on request.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private const string DashboardUrl = "http://localhost:5110";
    private const string HealthUrl = "http://localhost:5110/api/health";

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _openItem;
    private readonly System.Windows.Forms.Timer _healthTimer;
    private readonly HttpClient _http;

    public TrayApplicationContext()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Build context menu
        _statusItem = new ToolStripMenuItem("Service: Checking…") { Enabled = false };
        _openItem = new ToolStripMenuItem("Open Dashboard", null, OnOpenDashboard);
        var separator = new ToolStripSeparator();
        var exitItem = new ToolStripMenuItem("Exit", null, OnExit);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_openItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        // Tray icon — use embedded resource, fall back to SystemIcons if missing
        var icon = LoadEmbeddedIcon() ?? SystemIcons.Shield;

        _trayIcon = new NotifyIcon
        {
            Icon = icon,
            ContextMenuStrip = menu,
            Text = "KidMonitor",
            Visible = true,
        };

        // Double-click opens dashboard
        _trayIcon.DoubleClick += OnOpenDashboard;

        // Health poll timer (every 30 seconds)
        _healthTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _healthTimer.Tick += async (_, _) => await CheckHealthAsync();
        _healthTimer.Start();

        // Initial health check
        _ = CheckHealthAsync();
    }

    private static Icon? LoadEmbeddedIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("KidMonitor.Tray.Resources.kidmonitor.ico");
            return stream is not null ? new Icon(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task CheckHealthAsync()
    {
        bool reachable;
        try
        {
            var response = await _http.GetAsync(HealthUrl);
            reachable = response.IsSuccessStatusCode;
        }
        catch
        {
            reachable = false;
        }

        // Marshal back to UI thread
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _trayIcon.ContextMenuStrip.Invoke(() => UpdateStatus(reachable));
        }
        else
        {
            UpdateStatus(reachable);
        }
    }

    private void UpdateStatus(bool reachable)
    {
        _statusItem.Text = reachable ? "Service: Running ✓" : "Service: Unreachable ✗";
        _openItem.Enabled = reachable;
        _trayIcon.Text = reachable ? "KidMonitor — Running" : "KidMonitor — Unreachable";
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(DashboardUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser: {ex.Message}", "KidMonitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _healthTimer.Stop();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _healthTimer.Dispose();
            _trayIcon.Dispose();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
