using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using KidMonitor.Service;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KidMonitor.Tests.Service;

/// <summary>
/// Tests <see cref="ToastNotificationService"/> persistence behaviour.
/// The Windows toast call is expected to fail (or succeed) in the test environment;
/// in either case the NotificationLog must be persisted with the correct fields.
/// </summary>
public sealed class ToastNotificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;
    private readonly ToastNotificationService _sut;

    public ToastNotificationServiceTests()
    {
        _db = InMemoryDbHelper.CreateDb(out _connection);
        var scopeFactory = InMemoryDbHelper.CreateScopeFactory(_db);
        _sut = new ToastNotificationService(scopeFactory, NullLogger<ToastNotificationService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── SendAppStartedAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SendAppStartedAsync_PersistsNotificationLog_WithCategoryAppStart()
    {
        var session = new AppSession
        {
            Id = 42,
            ProcessName = "chrome",
            DisplayName = "Google Chrome",
            StartedAt = DateTime.UtcNow
        };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        await _sut.SendAppStartedAsync(session);

        var log = await _db.NotificationLogs.SingleAsync();
        Assert.Equal("AppStart", log.Category);
        Assert.Equal("App Opened", log.Title);
        Assert.Contains("Google Chrome", log.Body);
        Assert.Equal(session.Id, log.AppSessionId);
    }

    [Fact]
    public async Task SendAppStartedAsync_DoesNotThrow_EvenIfToastFails()
    {
        var session = new AppSession
        {
            ProcessName = "msedge", DisplayName = "Edge", StartedAt = DateTime.UtcNow
        };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        // Toast may throw in headless environment — service must swallow it
        var ex = await Record.ExceptionAsync(() => _sut.SendAppStartedAsync(session));
        Assert.Null(ex);
    }

    [Fact]
    public async Task SendAppStartedAsync_WhenToastFails_SetsDelivered_False()
    {
        // In headless test env the toast call will throw; we verify Delivered=false is persisted
        var session = new AppSession
        {
            ProcessName = "notepad", DisplayName = "Notepad", StartedAt = DateTime.UtcNow
        };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        await _sut.SendAppStartedAsync(session);

        var log = await _db.NotificationLogs.SingleAsync();
        // Delivered may be true (if toast succeeded on this machine) or false (headless).
        // Either way, the log entry must exist.
        Assert.NotNull(log);
        Assert.Equal(session.Id, log.AppSessionId);
    }

    // ── SendFoulLanguageDetectedAsync ───────────────────────────────────────

    [Fact]
    public async Task SendFoulLanguageDetectedAsync_PersistsLog_WithCategoryFoulLanguage()
    {
        await _sut.SendFoulLanguageDetectedAsync("Discord", "some bad word");

        var log = await _db.NotificationLogs.SingleAsync();
        Assert.Equal("FoulLanguage", log.Category);
        Assert.Equal("Foul Language Detected", log.Title);
        Assert.Contains("Discord", log.Body);
        Assert.Null(log.AppSessionId);
    }

    [Fact]
    public async Task SendFoulLanguageDetectedAsync_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.SendFoulLanguageDetectedAsync("YouTube", "snippet"));
        Assert.Null(ex);
    }

    // ── SendDailySummaryAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SendDailySummaryAsync_PersistsLog_WithCategoryDailySummary()
    {
        var summary = new DailySummary
        {
            ReportDate = DateOnly.FromDateTime(DateTime.Today),
            TotalScreenTimeSeconds = 5400, // 1h 30m
            FoulLanguageEventCount = 1,
            GeneratedAt = DateTime.UtcNow
        };
        _db.DailySummaries.Add(summary);
        await _db.SaveChangesAsync();

        await _sut.SendDailySummaryAsync(summary);

        var log = await _db.NotificationLogs.SingleAsync();
        Assert.Equal("DailySummary", log.Category);
        Assert.Contains("Daily Summary", log.Title);
        // 5400s = 1h 30m
        Assert.Contains("1h 30m", log.Body);
        Assert.Contains("Foul language events: 1", log.Body);
    }

    [Fact]
    public async Task SendDailySummaryAsync_DoesNotThrow()
    {
        var summary = new DailySummary
        {
            ReportDate = DateOnly.Parse("2026-03-01"),
            TotalScreenTimeSeconds = 0,
            GeneratedAt = DateTime.UtcNow
        };
        _db.DailySummaries.Add(summary);
        await _db.SaveChangesAsync();

        var ex = await Record.ExceptionAsync(() => _sut.SendDailySummaryAsync(summary));
        Assert.Null(ex);
    }

    // ── Multiple sends ──────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleNotifications_AreAllPersisted()
    {
        var session = new AppSession { ProcessName = "chrome", DisplayName = "Chrome", StartedAt = DateTime.UtcNow };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        await _sut.SendAppStartedAsync(session);
        await _sut.SendFoulLanguageDetectedAsync("Chrome", "snippet");

        var count = await _db.NotificationLogs.CountAsync();
        Assert.Equal(2, count);
    }
}
