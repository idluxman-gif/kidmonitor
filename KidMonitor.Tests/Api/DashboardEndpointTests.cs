using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;

namespace KidMonitor.Tests.Api;

/// <summary>
/// Integration tests for the read-only dashboard REST endpoints (WHA-27):
///
///   GET /api/dashboard               – today's aggregate (screen time, per-app, foul count)
///   GET /api/sessions?date=YYYY-MM-DD – AppSessions for a date
///   GET /api/summaries?days=N         – DailySummaries for the last N days
///   GET /api/events/language?date=    – LanguageDetectionEvents for a date
///
/// All endpoints require PIN auth; tests use <see cref="ApiTestFactory.CreateAuthenticatedClientAsync"/>.
/// </summary>
public sealed class DashboardEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public DashboardEndpointTests(ApiTestFactory factory) => _factory = factory;

    // ── GET /api/dashboard ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_ReturnsOk_WithAuthCookie()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_ReturnsJsonWithExpectedFields()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Dashboard response must carry: totalScreenTimeSeconds, appBreakdown, foulLanguageEventCount
        Assert.True(root.TryGetProperty("totalScreenTimeSeconds", out _),
            "Response missing 'totalScreenTimeSeconds'");
        Assert.True(root.TryGetProperty("appBreakdown", out _),
            "Response missing 'appBreakdown'");
        Assert.True(root.TryGetProperty("foulLanguageEventCount", out _),
            "Response missing 'foulLanguageEventCount'");
    }

    [Fact]
    public async Task GetDashboard_ReflectsSeedData_ForToday()
    {
        using var db = _factory.CreateDbContext();
        var today = DateTime.UtcNow.Date;

        db.AppSessions.Add(new AppSession
        {
            ProcessName = "chrome.exe",
            DisplayName = "Google Chrome",
            StartedAt = today.AddHours(9),
            EndedAt = today.AddHours(10),
            DurationSeconds = 3600
        });
        await db.SaveChangesAsync();

        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var total = doc.RootElement.GetProperty("totalScreenTimeSeconds").GetInt32();

        Assert.True(total >= 3600, $"Expected totalScreenTimeSeconds >= 3600, got {total}");
    }

    // ── GET /api/sessions ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSessions_ReturnsOk_WithAuthCookie()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/sessions?date={date}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSessions_ReturnsArray()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/sessions?date={date}");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetSessions_ReturnsSeedData_ForRequestedDate()
    {
        var sessionDate = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        using var db = _factory.CreateDbContext();
        db.AppSessions.Add(new AppSession
        {
            ProcessName = "notepad.exe",
            DisplayName = "Notepad",
            StartedAt = sessionDate,
            EndedAt = sessionDate.AddMinutes(30),
            DurationSeconds = 1800
        });
        await db.SaveChangesAsync();

        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/sessions?date=2026-01-15");
        var sessions = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);

        Assert.NotNull(sessions);
        Assert.True(sessions!.Length >= 1,
            "Expected at least one session for 2026-01-15");
        Assert.Contains(sessions,
            s => s.TryGetProperty("processName", out var pn)
                 && pn.GetString() == "notepad.exe");
    }

    [Fact]
    public async Task GetSessions_DefaultsToToday_WhenDateOmitted()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GET /api/summaries ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_ReturnsOk_WithAuthCookie()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/summaries?days=7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSummaries_ReturnsArray()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/summaries?days=30");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetSummaries_ReturnsSeedData_WithinRequestedWindow()
    {
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
        using var db = _factory.CreateDbContext();
        db.DailySummaries.Add(new DailySummary
        {
            ReportDate = reportDate,
            TotalScreenTimeSeconds = 7200,
            FoulLanguageEventCount = 2,
            AppBreakdownJson = "{\"chrome.exe\":7200}"
        });
        await db.SaveChangesAsync();

        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/summaries?days=7");
        var summaries = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);

        Assert.NotNull(summaries);
        Assert.True(summaries!.Length >= 1,
            "Expected at least one summary within the last 7 days");
    }

    // ── GET /api/events/language ────────────────────────────────────────────

    [Fact]
    public async Task GetLanguageEvents_ReturnsOk_WithAuthCookie()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/events/language?date={date}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLanguageEvents_ReturnsArray()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/events/language?date={date}");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetLanguageEvents_ReturnsSeedData_ForRequestedDate()
    {
        var eventDate = new DateTime(2026, 2, 10, 14, 0, 0, DateTimeKind.Utc);
        using var db = _factory.CreateDbContext();
        db.LanguageDetectionEvents.Add(new LanguageDetectionEvent
        {
            AppName = "YouTube",
            Source = "text",
            MatchedTerm = "badword",
            ContextSnippet = "...badword found in title...",
            DetectedAt = eventDate
        });
        await db.SaveChangesAsync();

        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/events/language?date=2026-02-10");
        var events = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);

        Assert.NotNull(events);
        Assert.True(events!.Length >= 1,
            "Expected at least one language event for 2026-02-10");
        Assert.Contains(events,
            e => e.TryGetProperty("matchedTerm", out var t)
                 && t.GetString() == "badword");
    }

    [Fact]
    public async Task GetLanguageEvents_DefaultsToToday_WhenDateOmitted()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/events/language");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
