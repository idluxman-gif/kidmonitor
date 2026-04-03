using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Tests.Data;

public sealed class KidMonitorDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;

    public KidMonitorDbContextTests()
    {
        _db = InMemoryDbHelper.CreateDb(out _connection);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<string?> ReadTextColumnAsync(string tableName, string columnName, int id)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {columnName} FROM {tableName} WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        return (string?)await command.ExecuteScalarAsync();
    }

    // ── AppSession ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AppSession_CanBeSavedAndRetrieved()
    {
        var session = new AppSession
        {
            ProcessName = "chrome",
            DisplayName = "Google Chrome",
            StartedAt = DateTime.UtcNow
        };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        var retrieved = await _db.AppSessions.FindAsync(session.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("chrome", retrieved.ProcessName);
        Assert.Equal("Google Chrome", retrieved.DisplayName);
        Assert.True(retrieved.Id > 0);
    }

    [Fact]
    public async Task AppSession_DurationSeconds_DefaultsToZero()
    {
        var session = new AppSession { ProcessName = "msedge", DisplayName = "Edge", StartedAt = DateTime.UtcNow };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        var retrieved = await _db.AppSessions.FindAsync(session.Id);

        Assert.Equal(0, retrieved!.DurationSeconds);
        Assert.Null(retrieved.EndedAt);
    }

    [Fact]
    public async Task AppSession_CanClose_Session()
    {
        var started = DateTime.UtcNow.AddMinutes(-5);
        var ended = DateTime.UtcNow;
        var session = new AppSession { ProcessName = "chrome", DisplayName = "Chrome", StartedAt = started };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        session.EndedAt = ended;
        session.DurationSeconds = (int)(ended - started).TotalSeconds;
        await _db.SaveChangesAsync();

        var retrieved = await _db.AppSessions.FindAsync(session.Id);
        Assert.NotNull(retrieved!.EndedAt);
        Assert.Equal(300, retrieved.DurationSeconds);
    }

    // ── NotificationLog ─────────────────────────────────────────────────────

    [Fact]
    public async Task NotificationLog_CanBeSavedWithoutAppSession()
    {
        var log = new NotificationLog
        {
            Category = "FoulLanguage",
            Title = "Foul Language Detected",
            Body = "Inappropriate content in Discord.",
            SentAt = DateTime.UtcNow,
            Delivered = true
        };
        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync();

        var retrieved = await _db.NotificationLogs.FindAsync(log.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("FoulLanguage", retrieved.Category);
        Assert.Null(retrieved.AppSessionId);
    }

    [Fact]
    public async Task NotificationLog_LinksToAppSession()
    {
        var session = new AppSession { ProcessName = "chrome", DisplayName = "Chrome", StartedAt = DateTime.UtcNow };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        var log = new NotificationLog
        {
            Category = "AppStart",
            Title = "App Opened",
            Body = "Chrome was opened.",
            AppSessionId = session.Id,
            SentAt = DateTime.UtcNow,
            Delivered = true
        };
        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync();

        var retrieved = await _db.NotificationLogs
            .Include(n => n.AppSession)
            .FirstAsync(n => n.Id == log.Id);

        Assert.Equal(session.Id, retrieved.AppSessionId);
        Assert.Equal("chrome", retrieved.AppSession!.ProcessName);
    }

    [Fact]
    public async Task NotificationLog_Body_IsEncryptedAtRest()
    {
        var log = new NotificationLog
        {
            Category = "ContentAlert",
            Title = "Content Alert",
            Body = "[YouTube] [text] 14:30:00 - Potential foul language detected.",
            SentAt = DateTime.UtcNow,
            Delivered = true,
        };
        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync();

        var storedBody = await ReadTextColumnAsync("NotificationLogs", "Body", log.Id);

        Assert.NotEqual(log.Body, storedBody);

        var retrieved = await _db.NotificationLogs.AsNoTracking().SingleAsync(n => n.Id == log.Id);
        Assert.Equal(log.Body, retrieved.Body);
    }

    [Fact]
    public async Task NotificationLog_AppSessionId_SetNullWhenSessionDeleted()
    {
        var session = new AppSession { ProcessName = "chrome", DisplayName = "Chrome", StartedAt = DateTime.UtcNow };
        _db.AppSessions.Add(session);
        await _db.SaveChangesAsync();

        var log = new NotificationLog
        {
            Category = "AppStart", Title = "T", Body = "B",
            AppSessionId = session.Id, SentAt = DateTime.UtcNow, Delivered = true
        };
        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync();

        _db.AppSessions.Remove(session);
        await _db.SaveChangesAsync();

        var retrieved = await _db.NotificationLogs.FindAsync(log.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.AppSessionId);
    }

    // ── DailySummary ────────────────────────────────────────────────────────

    [Fact]
    public async Task DailySummary_CanBeSavedAndRetrieved()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var summary = new DailySummary
        {
            ReportDate = date,
            TotalScreenTimeSeconds = 7200,
            AppBreakdownJson = "{\"chrome\":7200}",
            FoulLanguageEventCount = 2,
            GeneratedAt = DateTime.UtcNow
        };
        _db.DailySummaries.Add(summary);
        await _db.SaveChangesAsync();

        var retrieved = await _db.DailySummaries.FindAsync(summary.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(date, retrieved.ReportDate);
        Assert.Equal(7200, retrieved.TotalScreenTimeSeconds);
    }

    [Fact]
    public async Task DailySummary_UniqueIndex_RejectsSecondEntryForSameDate()
    {
        var date = DateOnly.Parse("2026-01-15");
        _db.DailySummaries.Add(new DailySummary { ReportDate = date, GeneratedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        _db.DailySummaries.Add(new DailySummary { ReportDate = date, GeneratedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    // ── ContentSession ──────────────────────────────────────────────────────

    [Fact]
    public async Task ContentSession_CanBeSavedAndRetrieved()
    {
        var cs = new ContentSession
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            ContentTitle = "Funny cats compilation",
            StartedAt = DateTime.UtcNow
        };
        _db.ContentSessions.Add(cs);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ContentSessions.FindAsync(cs.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("YouTube", retrieved.AppName);
        Assert.Equal(ContentType.VideoTitle, retrieved.ContentType);
    }

    [Fact]
    public async Task ContentSession_CanLinkToAppSession()
    {
        var appSession = new AppSession { ProcessName = "chrome", DisplayName = "Chrome", StartedAt = DateTime.UtcNow };
        _db.AppSessions.Add(appSession);
        await _db.SaveChangesAsync();

        var cs = new ContentSession
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            ContentTitle = "My video",
            AppSessionId = appSession.Id,
            StartedAt = DateTime.UtcNow
        };
        _db.ContentSessions.Add(cs);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ContentSessions
            .Include(s => s.AppSession)
            .FirstAsync(s => s.Id == cs.Id);

        Assert.Equal(appSession.Id, retrieved.AppSessionId);
        Assert.Equal("chrome", retrieved.AppSession!.ProcessName);
    }

    [Fact]
    public async Task ContentSession_SensitiveFields_AreEncryptedAtRest()
    {
        var session = new ContentSession
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            ContentTitle = "Funny cats compilation",
            ContentIdentifier = "https://youtube.example/watch?v=123",
            Channel = "Cat Channel",
            StartedAt = DateTime.UtcNow,
        };
        _db.ContentSessions.Add(session);
        await _db.SaveChangesAsync();

        var storedTitle = await ReadTextColumnAsync("ContentSessions", "ContentTitle", session.Id);
        var storedIdentifier = await ReadTextColumnAsync("ContentSessions", "ContentIdentifier", session.Id);
        var storedChannel = await ReadTextColumnAsync("ContentSessions", "Channel", session.Id);

        Assert.NotEqual(session.ContentTitle, storedTitle);
        Assert.NotEqual(session.ContentIdentifier, storedIdentifier);
        Assert.NotEqual(session.Channel, storedChannel);

        var retrieved = await _db.ContentSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(session.ContentTitle, retrieved.ContentTitle);
        Assert.Equal(session.ContentIdentifier, retrieved.ContentIdentifier);
        Assert.Equal(session.Channel, retrieved.Channel);
    }

    // ── ContentSnapshot ─────────────────────────────────────────────────────

    [Fact]
    public async Task ContentSnapshot_CanBeSavedAndRetrieved()
    {
        var snapshot = new ContentSnapshot
        {
            AppName = "WhatsApp Desktop",
            ContentType = ContentType.MessageText,
            CapturedText = "hey how are you",
            CapturedAt = DateTime.UtcNow
        };
        _db.ContentSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ContentSnapshots.FindAsync(snapshot.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("hey how are you", retrieved.CapturedText);
    }

    [Fact]
    public async Task ContentSnapshot_LinksToContentSession()
    {
        var cs = new ContentSession
        {
            AppName = "YouTube", ContentType = ContentType.VideoTitle,
            ContentTitle = "Test", StartedAt = DateTime.UtcNow
        };
        _db.ContentSessions.Add(cs);
        await _db.SaveChangesAsync();

        var snapshot = new ContentSnapshot
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            CapturedText = "Test video title",
            ContentSessionId = cs.Id,
            CapturedAt = DateTime.UtcNow
        };
        _db.ContentSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ContentSnapshots
            .Include(s => s.ContentSession)
            .FirstAsync(s => s.Id == snapshot.Id);

        Assert.Equal(cs.Id, retrieved.ContentSessionId);
        Assert.Equal("YouTube", retrieved.ContentSession!.AppName);
    }

    [Fact]
    public async Task ContentSnapshot_CapturedText_IsEncryptedAtRest()
    {
        var snapshot = new ContentSnapshot
        {
            AppName = "WhatsApp Desktop",
            ContentType = ContentType.MessageText,
            CapturedText = "hey how are you",
            Channel = "Alice",
            CapturedAt = DateTime.UtcNow,
        };
        _db.ContentSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        var storedText = await ReadTextColumnAsync("ContentSnapshots", "CapturedText", snapshot.Id);
        var storedChannel = await ReadTextColumnAsync("ContentSnapshots", "Channel", snapshot.Id);

        Assert.NotEqual(snapshot.CapturedText, storedText);
        Assert.NotEqual(snapshot.Channel, storedChannel);

        var retrieved = await _db.ContentSnapshots.AsNoTracking().SingleAsync(s => s.Id == snapshot.Id);
        Assert.Equal(snapshot.CapturedText, retrieved.CapturedText);
        Assert.Equal(snapshot.Channel, retrieved.Channel);
    }

    [Fact]
    public async Task ContentSnapshot_LegacyPlaintextRows_RemainReadable()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ContentSnapshots (
                ContentSessionId,
                AppName,
                ContentType,
                CapturedText,
                SourceUrl,
                Channel,
                CapturedAt
            )
            VALUES (
                NULL,
                $appName,
                $contentType,
                $capturedText,
                NULL,
                $channel,
                $capturedAt
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$appName", "Discord");
        command.Parameters.AddWithValue("$contentType", (int)ContentType.GameChat);
        command.Parameters.AddWithValue("$capturedText", "legacy plaintext");
        command.Parameters.AddWithValue("$channel", "general");
        command.Parameters.AddWithValue("$capturedAt", DateTime.UtcNow);

        var rowId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Row insert failed."));

        var snapshot = await _db.ContentSnapshots.AsNoTracking().SingleAsync(s => s.Id == (int)rowId);

        Assert.Equal("legacy plaintext", snapshot.CapturedText);
        Assert.Equal("general", snapshot.Channel);
    }

    [Fact]
    public async Task ContentSnapshot_ContentSessionId_SetNullWhenContentSessionDeleted()
    {
        var cs = new ContentSession
        {
            AppName = "Discord", ContentType = ContentType.GameChat,
            ContentTitle = "Game lobby", StartedAt = DateTime.UtcNow
        };
        _db.ContentSessions.Add(cs);
        await _db.SaveChangesAsync();

        var snapshot = new ContentSnapshot
        {
            AppName = "Discord",
            ContentType = ContentType.GameChat,
            CapturedText = "gg ez",
            ContentSessionId = cs.Id,
            CapturedAt = DateTime.UtcNow
        };
        _db.ContentSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        _db.ContentSessions.Remove(cs);
        await _db.SaveChangesAsync();

        var retrieved = await _db.ContentSnapshots.FindAsync(snapshot.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.ContentSessionId);
    }

    [Fact]
    public async Task ContentSession_Snapshots_NavigationProperty_Works()
    {
        var cs = new ContentSession
        {
            AppName = "YouTube", ContentType = ContentType.VideoTitle,
            ContentTitle = "Nature doc", StartedAt = DateTime.UtcNow
        };
        _db.ContentSessions.Add(cs);
        await _db.SaveChangesAsync();

        _db.ContentSnapshots.AddRange(
            new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "Title 1", ContentSessionId = cs.Id, CapturedAt = DateTime.UtcNow },
            new ContentSnapshot { AppName = "YouTube", ContentType = ContentType.VideoTitle, CapturedText = "Title 2", ContentSessionId = cs.Id, CapturedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var retrieved = await _db.ContentSessions
            .Include(s => s.Snapshots)
            .FirstAsync(s => s.Id == cs.Id);

        Assert.Equal(2, retrieved.Snapshots.Count);
    }

    [Fact]
    public async Task LanguageDetectionEvent_SensitiveFields_AreEncryptedAtRest()
    {
        var ev = new LanguageDetectionEvent
        {
            AppName = "Discord",
            Source = "text",
            MatchedTerm = "badword",
            ContextSnippet = "context with badword here",
            DetectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        _db.LanguageDetectionEvents.Add(ev);
        await _db.SaveChangesAsync();

        var storedTerm = await ReadTextColumnAsync("LanguageDetectionEvents", "MatchedTerm", ev.Id);
        var storedSnippet = await ReadTextColumnAsync("LanguageDetectionEvents", "ContextSnippet", ev.Id);

        Assert.NotEqual(ev.MatchedTerm, storedTerm);
        Assert.NotEqual(ev.ContextSnippet, storedSnippet);

        var retrieved = await _db.LanguageDetectionEvents.AsNoTracking().SingleAsync(e => e.Id == ev.Id);
        Assert.Equal(ev.MatchedTerm, retrieved.MatchedTerm);
        Assert.Equal(ev.ContextSnippet, retrieved.ContextSnippet);
    }
}
