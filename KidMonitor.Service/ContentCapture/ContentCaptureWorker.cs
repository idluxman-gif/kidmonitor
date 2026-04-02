using KidMonitor.Core.Data;
using KidMonitor.Core.Models;

namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// BackgroundService that orchestrates all <see cref="IContentCaptureAdapter"/> instances.
///
/// On each poll:
///   1. Enumerate visible windows via WindowEnumerator.
///   2. Ask each adapter whether it can handle a given window.
///   3. Capture a ContentSnapshot if the content has changed since last seen.
///   4. Open / close ContentSessions to group snapshots by content identity.
///   5. Persist everything to SQLite via KidMonitorDbContext.
/// </summary>
public class ContentCaptureWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IContentCaptureAdapter> _adapters;
    private readonly IConfiguration _config;
    private readonly ILogger<ContentCaptureWorker> _logger;

    // key: (processName + contentIdentifier) → open ContentSession.Id + last seen text
    private readonly Dictionary<string, (int SessionId, string LastText)> _openSessions =
        new(StringComparer.OrdinalIgnoreCase);

    public ContentCaptureWorker(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IContentCaptureAdapter> adapters,
        IConfiguration config,
        ILogger<ContentCaptureWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _adapters = adapters;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = _config.GetValue<int>("Monitoring:PollIntervalSeconds", 10);
        _logger.LogInformation("ContentCaptureWorker started, polling every {Poll}s.", pollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during content capture poll.");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
        }

        await CloseAllOpenSessionsAsync(CancellationToken.None);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var windows = WindowEnumerator.GetVisibleWindows();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();

        foreach (var window in windows)
        {
            foreach (var adapter in _adapters)
            {
                if (!adapter.CanCapture(window))
                    continue;

                ContentSnapshot? snapshot;
                try
                {
                    snapshot = adapter.TryCapture(window);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Adapter {Adapter} threw for window '{Title}'.",
                        adapter.GetType().Name, window.WindowTitle);
                    continue;
                }

                if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.CapturedText))
                    continue;

                var sessionKey = $"{window.ProcessName}|{snapshot.ContentType}|{snapshot.AppName}";
                seen.Add(sessionKey);

                if (_openSessions.TryGetValue(sessionKey, out var open))
                {
                    // Content changed — record a new snapshot
                    if (!string.Equals(open.LastText, snapshot.CapturedText, StringComparison.Ordinal))
                    {
                        snapshot.ContentSessionId = open.SessionId;
                        db.ContentSnapshots.Add(snapshot);
                        await db.SaveChangesAsync(ct);
                        _openSessions[sessionKey] = (open.SessionId, snapshot.CapturedText);

                        _logger.LogDebug("Content changed [{App}]: {Text}", snapshot.AppName, snapshot.CapturedText);
                    }
                }
                else
                {
                    // New content session
                    var session = new ContentSession
                    {
                        AppName = snapshot.AppName,
                        ContentType = snapshot.ContentType,
                        ContentTitle = snapshot.CapturedText,
                        Channel = snapshot.Channel,
                        StartedAt = DateTime.UtcNow,
                    };
                    db.ContentSessions.Add(session);
                    await db.SaveChangesAsync(ct);

                    snapshot.ContentSessionId = session.Id;
                    db.ContentSnapshots.Add(snapshot);
                    await db.SaveChangesAsync(ct);

                    _openSessions[sessionKey] = (session.Id, snapshot.CapturedText);
                    _logger.LogInformation("Content session opened [{App}]: {Text}", snapshot.AppName, snapshot.CapturedText);
                }

                break; // Only the first matching adapter wins per window
            }
        }

        // Close sessions whose window is no longer visible
        var stale = _openSessions.Keys.Where(k => !seen.Contains(k)).ToList();
        if (stale.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var key in stale)
            {
                var (sessionId, _) = _openSessions[key];
                var session = await db.ContentSessions.FindAsync(new object[] { sessionId }, ct);
                if (session is not null)
                {
                    session.EndedAt = now;
                    session.DurationSeconds = (int)(now - session.StartedAt).TotalSeconds;
                    _logger.LogInformation("Content session closed [{App}] after {Sec}s.",
                        session.AppName, session.DurationSeconds);
                }
                _openSessions.Remove(key);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task CloseAllOpenSessionsAsync(CancellationToken ct)
    {
        if (_openSessions.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
        var now = DateTime.UtcNow;

        foreach (var (_, (sessionId, _)) in _openSessions)
        {
            var session = await db.ContentSessions.FindAsync(new object[] { sessionId }, ct);
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
