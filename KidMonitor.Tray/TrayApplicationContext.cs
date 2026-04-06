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
    private readonly ToolStripMenuItem _pairWithParentItem;
    private readonly System.Windows.Forms.Timer _healthTimer;
    private readonly HttpClient _http;
    private readonly HealthPoller _healthPoller;
    private CancellationTokenSource? _pairingCancellation;
    private PairingDialog? _pairingDialog;
    private TrayPairingRuntime? _pairingRuntime;

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
        _pairWithParentItem = new ToolStripMenuItem("Pair with parent app", null, OnPairWithParentApp);
        var separator = new ToolStripSeparator();
        var exitItem = new ToolStripMenuItem("Exit", null, OnExit);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_openItem);
        menu.Items.Add(_pairWithParentItem);
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

    private async void OnPairWithParentApp(object? sender, EventArgs e)
    {
        if (_pairingDialog is not null && !_pairingDialog.IsDisposed)
        {
            _pairingDialog.Activate();
            return;
        }

        _pairWithParentItem.Enabled = false;

        try
        {
            _pairingRuntime = TrayPairingRuntimeFactory.TryCreate();
            if (_pairingRuntime is null)
            {
                ShowWarning(
                    "Cloud pairing is not configured. Set CloudApi:BaseUrl in C:\\ProgramData\\KidMonitor\\appsettings.json and try again.");
                return;
            }

            var session = await _pairingRuntime.Coordinator
                .StartAsync(TrayDeviceIdentity.ForCurrentMachine(), CancellationToken.None)
                .ConfigureAwait(true);

            _pairingCancellation = new CancellationTokenSource();
            _pairingDialog = new PairingDialog(session);
            _pairingDialog.FormClosed += OnPairingDialogClosed;
            _pairingDialog.Show();

            _ = RunPairingFlowAsync(_pairingRuntime, session, _pairingDialog, _pairingCancellation.Token);
        }
        catch (Exception ex)
        {
            _pairingRuntime?.Dispose();
            _pairingRuntime = null;
            ShowWarning($"Could not start pairing: {ex.Message}");
        }
        finally
        {
            if (_pairingDialog is null || _pairingDialog.IsDisposed)
            {
                _pairWithParentItem.Enabled = true;
            }
        }
    }

    private async Task RunPairingFlowAsync(
        TrayPairingRuntime pairingRuntime,
        TrayPairingSession session,
        PairingDialog dialog,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await pairingRuntime.Coordinator
                .WaitForConfirmationAsync(session, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested || dialog.IsDisposed)
            {
                return;
            }

            switch (result.Status)
            {
                case TrayPairingCompletionStatus.Confirmed:
                    dialog.ShowConfirmed(result.DeviceName ?? session.DeviceName);
                    _trayIcon.ShowBalloonTip(
                        5000,
                        "KidMonitor",
                        $"{result.DeviceName ?? session.DeviceName} is now paired with the parent app.",
                        ToolTipIcon.Info);
                    break;

                case TrayPairingCompletionStatus.Expired:
                    dialog.ShowExpired("The pairing code expired. Start a new pairing session from the tray menu.");
                    break;

                case TrayPairingCompletionStatus.TimedOut:
                    dialog.ShowExpired("The pairing session timed out. Start a new pairing session from the tray menu.");
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected pairing completion status: {result.Status}.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!dialog.IsDisposed)
            {
                dialog.ShowError($"Pairing failed: {ex.Message}");
            }
        }
    }

    private void OnPairingDialogClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is PairingDialog dialog)
        {
            dialog.FormClosed -= OnPairingDialogClosed;
        }

        _pairingCancellation?.Cancel();
        _pairingCancellation?.Dispose();
        _pairingCancellation = null;

        _pairingRuntime?.Dispose();
        _pairingRuntime = null;

        _pairingDialog = null;
        _pairWithParentItem.Enabled = true;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _healthTimer.Stop();
        _pairingDialog?.Close();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _healthTimer.Dispose();
            _pairingCancellation?.Dispose();
            _pairingRuntime?.Dispose();
            _trayIcon.Dispose();
            _http.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "KidMonitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
