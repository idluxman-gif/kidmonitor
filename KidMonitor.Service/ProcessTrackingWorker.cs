using System.Diagnostics;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service;

/// <summary>
/// Polls running processes every N seconds and records app usage sessions.
/// Detects when a tracked app starts or stops and persists the session to SQLite.
/// </summary>
public class ProcessTrackingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MonitoringOptions> _options;
    private readonly ILogger<ProcessTrackingWorker> _logger;
    private readonly INotificationService _notifications;

    // processName (lower) → open AppSession.Id
    private readonly Dictionary<string, int> _openSessions = new(StringComparer.OrdinalIgnoreCase);

    public ProcessTrackingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MonitoringOptions> options,
        ILogger<ProcessTrackingWorker> logger,
        INotificationService notifications)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = _options.Value.PollIntervalSeconds;
        var trackedApps = _options.Value.TrackedApps.Count > 0
            ? _options.Value.TrackedApps
            : new List<TrackedAppConfig>
            {
                new() { ProcessName = "chrome", DisplayName = "Google Chrome" },
                new() { ProcessName = "msedge", DisplayName = "Microsoft Edge" },
                new() { ProcessName = "firefox", DisplayName = "Firefox" },
                new() { ProcessName = "WhatsApp", DisplayName = "WhatsApp Desktop" },
            };

        _logger.LogInformation("ProcessTrackingWorker started. Tracking {Count} apps, polling every {Poll}s.",
            trackedApps.Count, pollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(trackedApps, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during process poll.");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
        }

        // Close any open sessions on shutdown
        await CloseAllOpenSessionsAsync(stoppingToken);
    }

    private async Task PollAsync(List<TrackedAppConfig> trackedApps, CancellationToken ct)
    {
        var runningNames = Process.GetProcesses()
            .Select(p => p.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();

        foreach (var app in trackedApps)
        {
            var isRunning = runningNames.Contains(app.ProcessName);

            if (isRunning && !_openSessions.ContainsKey(app.ProcessName))
            {
                // App just started
                var session = new AppSession
                {
                    ProcessName = app.ProcessName,
                    DisplayName = app.DisplayName,
                    StartedAt = DateTime.UtcNow
                };
                db.AppSessions.Add(session);
                await db.SaveChangesAsync(ct);
                _openSessions[app.ProcessName] = session.Id;

                _logger.LogInformation("App started: {App}", app.DisplayName);
                await _notifications.SendAppStartedAsync(session, ct);
            }
            else if (!isRunning && _openSessions.TryGetValue(app.ProcessName, out var sessionId))
            {
                // App just stopped
                var session = await db.AppSessions.FindAsync(new object[] { sessionId }, ct);
                if (session is not null)
                {
                    session.EndedAt = DateTime.UtcNow;
                    session.DurationSeconds = (int)(session.EndedAt.Value - session.StartedAt).TotalSeconds;
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("App stopped: {App} ({Sec}s)", app.DisplayName, session.DurationSeconds);
                }
                _openSessions.Remove(app.ProcessName);
            }
        }
    }

    private async Task CloseAllOpenSessionsAsync(CancellationToken ct)
    {
        if (_openSessions.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
        var now = DateTime.UtcNow;

        foreach (var (processName, sessionId) in _openSessions)
        {
            var session = await db.AppSessions.FindAsync(new object[] { sessionId }, ct);
            if (session is not null)
            {
                session.EndedAt = now;
                session.DurationSeconds = (int)(now - session.StartedAt).TotalSeconds;
            }
        }
        await db.SaveChangesAsync(ct);
        _openSessions.Clear();
    }

}
