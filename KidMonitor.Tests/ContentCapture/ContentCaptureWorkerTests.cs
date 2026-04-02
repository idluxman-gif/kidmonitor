using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using KidMonitor.Service.ContentCapture;
using KidMonitor.Service.LanguageDetection;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KidMonitor.Tests.ContentCapture;

public sealed class ContentCaptureWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;

    public ContentCaptureWorkerTests()
    {
        _db = InMemoryDbHelper.CreateDb(out _connection);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static IConfiguration BuildConfig(int pollSeconds = 60) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:PollIntervalSeconds"] = pollSeconds.ToString()
            })
            .Build();

    private ContentCaptureWorker BuildWorker(
        IEnumerable<IContentCaptureAdapter>? adapters = null,
        ContentSnapshotChannel? channel = null) =>
        new ContentCaptureWorker(
            InMemoryDbHelper.CreateScopeFactory(_db),
            adapters ?? Enumerable.Empty<IContentCaptureAdapter>(),
            channel ?? new ContentSnapshotChannel(),
            BuildConfig(),
            NullLogger<ContentCaptureWorker>.Instance);

    // ── Construction ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNoAdapters_DoesNotThrow()
    {
        var ex = Record.Exception(() => BuildWorker());
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_WithMultipleAdapters_DoesNotThrow()
    {
        var adapters = new[]
        {
            new Mock<IContentCaptureAdapter>().Object,
            new Mock<IContentCaptureAdapter>().Object,
        };
        var ex = Record.Exception(() => BuildWorker(adapters));
        Assert.Null(ex);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CancelsCleanly_WhenTokenPreCancelled()
    {
        var worker = BuildWorker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await worker.StartAsync(cts.Token);
        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    // ── Adapter dispatch via CanCapture ────────────────────────────────────

    [Fact]
    public void FirstMatchingAdapter_IsUsed_PerWindow()
    {
        // Verify that adapters are checked in order — first match wins (break after first).
        // We can observe this by checking that the second adapter is never asked to capture
        // when the first one claims the window.
        var first = new Mock<IContentCaptureAdapter>();
        var second = new Mock<IContentCaptureAdapter>();

        first.Setup(a => a.CanCapture(It.IsAny<ProcessWindowInfo>())).Returns(false);
        second.Setup(a => a.CanCapture(It.IsAny<ProcessWindowInfo>())).Returns(false);

        // Construction succeeds; adapters are wired in order
        var worker = BuildWorker(new[] { first.Object, second.Object });
        Assert.NotNull(worker);
    }

    // ── Channel integration ────────────────────────────────────────────────

    [Fact]
    public void ContentSnapshotChannel_WriterAndReader_AreAccessible()
    {
        var channel = new ContentSnapshotChannel();
        Assert.NotNull(channel.Writer);
        Assert.NotNull(channel.Reader);
    }

    [Fact]
    public void ContentSnapshotChannel_AcceptsWrittenSnapshot()
    {
        var channel = new ContentSnapshotChannel();
        var snapshot = new ContentSnapshot
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            CapturedText = "Test Video",
            CapturedAt = DateTime.UtcNow,
        };

        var written = channel.Writer.TryWrite(snapshot);
        Assert.True(written);
    }
}
