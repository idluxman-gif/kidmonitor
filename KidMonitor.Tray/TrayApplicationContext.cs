using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;

namespace KidMonitor.Tray;

/// <summary>
/// WinForms ApplicationContext that provides the system tray icon for KidMonitor.
/// Polls the local dashboard API to display service health status.
/// Opens the browser to the dashboard on request.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private const string DashboardUrl = "http://localhost:5110";

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _openItem;
    private readonly System.Windows.Forms.Timer _healthTimer;
    private readonly HttpClient _http;
    private readonly HealthPoller _healthPoller;

    public TrayApplicationContext()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(DashboardUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };
        _healthPoller = new HealthPoller(_http, NullLogger<HealthPoller>.Instance);

        _statusItem = new ToolStripMenuItem("Service: Checking...") { Enabled = false };
        _openItem = new ToolStripMenuItem("Open Dashboard", null, OnOpenDashboard);
        var separator = new ToolStripSeparator();
        var exitItem = new ToolStripMenuItem("Exit", null, OnExit);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_openItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        var icon = LoadEmbeddedIcon() ?? SystemIcons.Shield;

        _trayIcon = new NotifyIcon
        {
            Icon = icon,
            ContextMenuStrip = menu,
            Text = "KidMonitor",
            Visible = true,
        };

        _trayIcon.DoubleClick += OnOpenDashboard;

        _healthTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _healthTimer.Tick += async (_, _) => await RefreshHealthAsync();
        _healthTimer.Start();

        _ = RefreshHealthAsync();
    }

    private static Icon? LoadEmbeddedIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("KidMonitor.Tray.Resources.kidmonitor.ico");
            return stream is not null ? new Icon(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task RefreshHealthAsync()
    {
        var reachable = await _healthPoller.CheckAsync(CancellationToken.None);

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
        _statusItem.Text = reachable ? "Service: Running" : "Service: Unreachable";
        _openItem.Enabled = reachable;
        _trayIcon.Text = reachable ? "KidMonitor - Running" : "KidMonitor - Unreachable";
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
