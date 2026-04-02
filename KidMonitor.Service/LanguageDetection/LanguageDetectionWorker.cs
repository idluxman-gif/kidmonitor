using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service.LanguageDetection;

/// <summary>
/// BackgroundService that consumes <see cref="ContentSnapshot"/> events from
/// <see cref="ContentSnapshotChannel"/> and runs them through
/// <see cref="IFoulLanguageDetector"/>.
///
/// When a match is found:
///   1. A <see cref="LanguageDetectionEvent"/> is persisted to SQLite.
///   2. <see cref="INotificationService.SendFoulLanguageDetectedAsync"/> is called.
///
/// Audio transcription for active YouTube sessions is delegated to
/// <see cref="WhisperTranscriptionService"/> when <c>AudioEnabled</c> is true.
/// </summary>
public class LanguageDetectionWorker : BackgroundService
{
    private readonly ContentSnapshotChannel _channel;
    private readonly IFoulLanguageDetector _detector;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notifications;
    private readonly IOptionsMonitor<MonitoringOptions> _options;
    private readonly WhisperTranscriptionService _transcriber;
    private readonly ILogger<LanguageDetectionWorker> _logger;

    public LanguageDetectionWorker(
        ContentSnapshotChannel channel,
        IFoulLanguageDetector detector,
        IServiceScopeFactory scopeFactory,
        INotificationService notifications,
        IOptionsMonitor<MonitoringOptions> options,
        WhisperTranscriptionService transcriber,
        ILogger<LanguageDetectionWorker> logger)
    {
        _channel = channel;
        _detector = detector;
        _scopeFactory = scopeFactory;
        _notifications = notifications;
        _options = options;
        _transcriber = transcriber;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LanguageDetectionWorker started.");

        // Start audio monitoring on a separate low-priority thread if configured.
        Task? audioTask = null;
        if (_options.CurrentValue.LanguageDetection.AudioEnabled)
        {
            audioTask = Task.Factory.StartNew(
                () => RunAudioMonitoringAsync(stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                PriorityTaskScheduler.BelowNormal)
                .Unwrap();
        }

        await foreach (var snapshot in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessSnapshotAsync(snapshot, "text", stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing snapshot from {App}.", snapshot.AppName);
            }
        }

        if (audioTask is not null)
        {
            try { await audioTask; }
            catch (OperationCanceledException) { }
        }
    }

    private async Task ProcessSnapshotAsync(ContentSnapshot snapshot, string source, CancellationToken ct)
    {
        var matches = _detector.Scan(snapshot.CapturedText, snapshot.AppName);
        if (matches.Count == 0)
            return;

        _logger.LogWarning(
            "Foul language detected [{Source}] in {App}: {Matches} match(es). First: '{Term}'.",
            source, snapshot.AppName, matches.Count, matches[0].MatchedTerm);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();

        foreach (var match in matches)
        {
            var ev = new LanguageDetectionEvent
            {
                ContentSessionId = snapshot.ContentSessionId,
                AppName = snapshot.AppName,
                Source = source,
                MatchedTerm = match.MatchedTerm,
                ContextSnippet = match.ContextSnippet,
                DetectedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            db.LanguageDetectionEvents.Add(ev);
        }
        await db.SaveChangesAsync(ct);

        // One notification per snapshot (aggregate multiple matches into first snippet).
        await _notifications.SendFoulLanguageDetectedAsync(
            snapshot.AppName,
            matches[0].ContextSnippet,
            ct);
    }

    // ── Audio monitoring ───────────────────────────────────────────────────────

    private async Task RunAudioMonitoringAsync(CancellationToken ct)
    {
        _logger.LogInformation("Audio monitoring started (YouTube loopback transcription).");

        var opts = _options.CurrentValue.LanguageDetection;
        var windowSec = Math.Max(4, opts.AudioWindowSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var transcript = await _transcriber.CaptureAndTranscribeAsync(
                    TimeSpan.FromSeconds(windowSec), ct);

                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    var fake = new ContentSnapshot
                    {
                        AppName = "YouTube (audio)",
                        ContentType = ContentType.VideoTitle,
                        CapturedText = transcript,
                        CapturedAt = DateTime.UtcNow,
                    };
                    await ProcessSnapshotAsync(fake, "audio", ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in audio monitoring loop; pausing 30s.");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }

        _logger.LogInformation("Audio monitoring stopped.");
    }
}

/// <summary>
/// Minimal task scheduler that runs tasks at BelowNormal thread priority.
/// </summary>
file sealed class PriorityTaskScheduler : TaskScheduler
{
    public static readonly PriorityTaskScheduler BelowNormal =
        new(ThreadPriority.BelowNormal);

    private readonly ThreadPriority _priority;

    private PriorityTaskScheduler(ThreadPriority priority) => _priority = priority;

    protected override IEnumerable<Task>? GetScheduledTasks() => null;

    protected override void QueueTask(Task task)
    {
        var t = new Thread(() => TryExecuteTask(task))
        {
            IsBackground = true,
            Priority = _priority,
        };
        t.Start();
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        => TryExecuteTask(task);
}
