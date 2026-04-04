using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using KidMonitor.Service;
using KidMonitor.Service.Cloud;
using KidMonitor.Service.LanguageDetection;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace KidMonitor.Tests.LanguageDetection;

public sealed class LanguageDetectionWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;

    public LanguageDetectionWorkerTests()
    {
        _db = InMemoryDbHelper.CreateDb(out _connection);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static IOptionsMonitor<MonitoringOptions> BuildOptions(
        int cooldownSeconds = 60,
        bool audioEnabled = false)
    {
        var options = new MonitoringOptions
        {
            LanguageDetection = new LanguageDetectionOptions
            {
                Enabled = true,
                AudioEnabled = audioEnabled,
            },
            Notifications = new NotificationOptions
            {
                FoulLanguageCooldownSeconds = cooldownSeconds,
            }
        };

        var monitor = new Mock<IOptionsMonitor<MonitoringOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        monitor
            .Setup(m => m.OnChange(It.IsAny<Action<MonitoringOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());
        return monitor.Object;
    }

    private static WhisperTranscriptionService BuildTranscriber()
    {
        // Create a real WhisperTranscriptionService; audio is disabled in all tests
        // so CaptureAndTranscribeAsync is never called.
        var options = new MonitoringOptions
        {
            LanguageDetection = new LanguageDetectionOptions { AudioEnabled = false }
        };
        var monitor = new Mock<IOptionsMonitor<MonitoringOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        monitor
            .Setup(m => m.OnChange(It.IsAny<Action<MonitoringOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());
        return new WhisperTranscriptionService(
            monitor.Object,
            NullLogger<WhisperTranscriptionService>.Instance);
    }

    private LanguageDetectionWorker BuildWorker(
        ContentSnapshotChannel channel,
        MonitoringEventChannel monitoringEventChannel,
        IFoulLanguageDetector detector,
        Mock<INotificationService> notificationMock,
        int cooldownSeconds = 60)
    {
        return new LanguageDetectionWorker(
            channel,
            monitoringEventChannel,
            detector,
            InMemoryDbHelper.CreateScopeFactory(_db),
            notificationMock.Object,
            BuildOptions(cooldownSeconds),
            BuildTranscriber(),
            NullLogger<LanguageDetectionWorker>.Instance);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CancelsCleanly_WhenTokenPreCancelled()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector.Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Array.Empty<DetectionMatch>());

        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            new Mock<INotificationService>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await worker.StartAsync(cts.Token);
        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    // ── DB persistence ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessSnapshot_PersistsLanguageDetectionEvent_ToDb()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[] { new DetectionMatch("badword", "context with badword here") });

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(CancellationToken.None);

        // Write a snapshot to the channel
        var snapshot = new ContentSnapshot
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            CapturedText = "Video with a badword in title",
            CapturedAt = DateTime.UtcNow,
        };
        channel.Writer.TryWrite(snapshot);

        // Give the worker time to process
        await Task.Delay(300, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        var events = await _db.LanguageDetectionEvents.ToListAsync();
        Assert.Single(events);
        Assert.Equal("YouTube", events[0].AppName);
        Assert.Equal("badword", events[0].MatchedTerm);
        Assert.Equal("text", events[0].Source);
    }

    [Fact]
    public async Task ProcessSnapshot_PersistsMultipleMatches_AsMultipleEvents()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[]
            {
                new DetectionMatch("word1", "context1"),
                new DetectionMatch("word2", "context2"),
            });

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new ContentSnapshot
        {
            AppName = "Discord",
            ContentType = ContentType.GameChat,
            CapturedText = "word1 and word2",
            CapturedAt = DateTime.UtcNow,
        });

        await Task.Delay(300, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        var events = await _db.LanguageDetectionEvents.ToListAsync();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ProcessSnapshot_DoesNotPersistEvent_WhenNoMatches()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Array.Empty<DetectionMatch>());

        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            new Mock<INotificationService>());

        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new ContentSnapshot
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            CapturedText = "clean video title",
            CapturedAt = DateTime.UtcNow,
        });

        await Task.Delay(200, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Empty(await _db.LanguageDetectionEvents.ToListAsync());
    }

    // ── Throttle logic ─────────────────────────────────────────────────────

    [Fact]
    public async Task Throttle_SendsOneNotification_ForTwoRapidSnapshotsFromSameApp()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[] { new DetectionMatch("badword", "ctx") });

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 60s cooldown — two rapid snapshots should only fire one notification
        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 60);

        await worker.StartAsync(CancellationToken.None);

        var s1 = new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "bad 1", CapturedAt = DateTime.UtcNow };
        var s2 = new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "bad 2", CapturedAt = DateTime.UtcNow };
        channel.Writer.TryWrite(s1);
        channel.Writer.TryWrite(s2);

        await Task.Delay(400, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        notificationMock.Verify(
            n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "Second snapshot should be throttled within 60s cooldown");
    }

    [Fact]
    public async Task Throttle_SendsNotification_ForDifferentApps()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[] { new DetectionMatch("bad", "ctx") });

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 60s cooldown — but different apps should each send their own notification
        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 60);

        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "bad yt", CapturedAt = DateTime.UtcNow });
        channel.Writer.TryWrite(new ContentSnapshot { AppName = "Discord", ContentType = ContentType.GameChat, CapturedText = "bad dc", CapturedAt = DateTime.UtcNow });

        await Task.Delay(400, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Two different apps → two notifications (throttle key includes AppName)
        notificationMock.Verify(
            n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "Each distinct app should send its own notification");
    }

    [Fact]
    public async Task Throttle_SendsTwoNotifications_WhenCooldownIsZero()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[] { new DetectionMatch("bad", "ctx") });

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 0s cooldown — every snapshot should send a notification
        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 0);

        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "bad 1", CapturedAt = DateTime.UtcNow });
        await Task.Delay(100, CancellationToken.None);
        channel.Writer.TryWrite(new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "bad 2", CapturedAt = DateTime.UtcNow });

        await Task.Delay(400, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        notificationMock.Verify(
            n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "With 0s cooldown every snapshot should trigger a notification");
    }

    // ── ContentAlertEvent fields ───────────────────────────────────────────

    [Fact]
    public async Task ProcessSnapshot_ContentAlertEvent_HasCorrectAppNameAndSource()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[] { new DetectionMatch("bad", "context snippet") });

        ContentAlertEvent? captured = null;
        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ContentAlertEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 0);

        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new ContentSnapshot
        {
            AppName = "WhatsApp Desktop",
            ContentType = ContentType.MessageText,
            CapturedText = "message with bad content",
            CapturedAt = DateTime.UtcNow,
        });

        await Task.Delay(300, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("WhatsApp Desktop", captured!.AppName);
        Assert.Equal("text", captured.Source);
    }

    [Fact]
    public async Task ProcessSnapshot_EnqueuesMonitoringEvent_ForCloudSync()
    {
        var channel = new ContentSnapshotChannel();
        var monitoringEventChannel = new MonitoringEventChannel();
        var detector = new Mock<IFoulLanguageDetector>();
        detector
            .Setup(d => d.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new[] { new DetectionMatch("badword", "context snippet") });

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyContentAlertAsync(It.IsAny<ContentAlertEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(
            channel,
            monitoringEventChannel,
            detector.Object,
            notificationMock,
            cooldownSeconds: 0);

        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new ContentSnapshot
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            CapturedText = "video title with badword",
            CapturedAt = DateTime.UtcNow,
        });

        await Task.Delay(300, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(monitoringEventChannel.Reader.TryRead(out var monitoringEvent));
        Assert.NotNull(monitoringEvent);
        Assert.Equal("foul_language_detected", monitoringEvent!.EventType);
        Assert.Equal("YouTube", monitoringEvent.Metadata["appName"]);
        Assert.Equal("badword", monitoringEvent.Metadata["matchedTerm"]);
        Assert.Equal("text", monitoringEvent.Metadata["source"]);
    }
}
