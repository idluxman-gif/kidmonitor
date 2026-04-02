using System.Reflection;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using KidMonitor.Service;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KidMonitor.Tests.Service;

public sealed class DailySummaryWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;

    public DailySummaryWorkerTests()
    {
        _db = InMemoryDbHelper.CreateDb(out _connection);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static IConfiguration BuildConfig(string summaryTime = "20:00") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:DailySummaryTimeLocal"] = summaryTime
            })
            .Build();

    private DailySummaryWorker BuildWorker(
        IConfiguration? config = null,
        Mock<INotificationService>? notificationMock = null)
    {
        var scopeFactory = InMemoryDbHelper.CreateScopeFactory(_db);
        var notifications = (notificationMock ?? new Mock<INotificationService>()).Object;
        return new DailySummaryWorker(
            scopeFactory,
            config ?? BuildConfig(),
            NullLogger<DailySummaryWorker>.Instance,
            notifications);
    }

    // ── BuildHtml via reflection ────────────────────────────────────────────

    private static string InvokeBuildHtml(
        DateOnly date, int totalSeconds,
        Dictionary<string, int> breakdown, int foulCount)
    {
        var method = typeof(DailySummaryWorker)
            .GetMethod("BuildHtml", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildHtml not found via reflection");

        return (string)method.Invoke(null, new object[] { date, totalSeconds, breakdown, foulCount })!;
    }

    [Fact]
    public void BuildHtml_ContainsDateInTitle()
    {
        var date = DateOnly.Parse("2026-04-02");
        var html = InvokeBuildHtml(date, 0, new Dictionary<string, int>(), 0);

        Assert.Contains("April 2, 2026", html);
    }

    [Fact]
    public void BuildHtml_DisplaysCorrectScreenTime()
    {
        // 7260 seconds = 2h 1m
        var html = InvokeBuildHtml(
            DateOnly.Parse("2026-01-01"), 7260,
            new Dictionary<string, int> { { "chrome", 7260 } }, 0);

        Assert.Contains("2h 1m", html);
    }

    [Fact]
    public void BuildHtml_ContainsAppBreakdown()
    {
        var breakdown = new Dictionary<string, int>
        {
            { "chrome", 3600 },
            { "WhatsApp", 1800 }
        };
        var html = InvokeBuildHtml(DateOnly.Parse("2026-01-01"), 5400, breakdown, 0);

        Assert.Contains("chrome", html);
        Assert.Contains("WhatsApp", html);
        // chrome = 1h 0m; WhatsApp = 0h 30m
        Assert.Contains("1h 0m", html);
        Assert.Contains("0h 30m", html);
    }

    [Fact]
    public void BuildHtml_ShowsFoulLanguageWarning_WhenCountPositive()
    {
        var html = InvokeBuildHtml(DateOnly.Parse("2026-01-01"), 0, new Dictionary<string, int>(), 3);

        Assert.Contains("Foul language events", html);
        Assert.Contains("3", html);
    }

    [Fact]
    public void BuildHtml_OmitsFoulLanguageSection_WhenCountZero()
    {
        var html = InvokeBuildHtml(DateOnly.Parse("2026-01-01"), 0, new Dictionary<string, int>(), 0);

        Assert.DoesNotContain("Foul language events", html);
    }

    [Fact]
    public void BuildHtml_IsValidHtml_WithDoctype()
    {
        var html = InvokeBuildHtml(DateOnly.Parse("2026-01-01"), 0, new Dictionary<string, int>(), 0);

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
    }

    [Fact]
    public void BuildHtml_AppBreakdown_OrderedByDurationDescending()
    {
        var breakdown = new Dictionary<string, int>
        {
            { "notepad", 60 },
            { "chrome", 3600 },
            { "WhatsApp", 1800 }
        };
        var html = InvokeBuildHtml(DateOnly.Parse("2026-01-01"), 5460, breakdown, 0);

        // chrome (highest) should appear before WhatsApp, which before notepad
        var chromeIdx = html.IndexOf("chrome", StringComparison.OrdinalIgnoreCase);
        var whatsappIdx = html.IndexOf("WhatsApp", StringComparison.OrdinalIgnoreCase);
        var notepadIdx = html.IndexOf("notepad", StringComparison.OrdinalIgnoreCase);

        Assert.True(chromeIdx < whatsappIdx, "chrome should appear before WhatsApp in the breakdown table");
        Assert.True(whatsappIdx < notepadIdx, "WhatsApp should appear before notepad in the breakdown table");
    }

    // ── ExecuteAsync lifecycle ──────────────────────────────────────────────

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

    [Fact]
    public async Task ExecuteAsync_DoesNotSendNotification_WhenTodaySummaryAlreadyExists()
    {
        // Arrange: pre-populate today's summary
        var today = DateOnly.FromDateTime(DateTime.Today);
        _db.DailySummaries.Add(new DailySummary
        {
            ReportDate = today,
            TotalScreenTimeSeconds = 1800,
            GeneratedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var notificationMock = new Mock<INotificationService>();
        // Set summary time to "00:00" so the worker enters the "already past" branch on any real clock
        var worker = BuildWorker(BuildConfig(summaryTime: "00:00"), notificationMock);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await worker.StartAsync(CancellationToken.None);
        try { await Task.Delay(400, cts.Token); } catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        // Assert: since today's summary exists, SendDailySummaryAsync should NOT have been called
        notificationMock.Verify(
            n => n.SendDailySummaryAsync(It.IsAny<DailySummary>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
