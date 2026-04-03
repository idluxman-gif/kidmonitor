using System.Text.Json;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service.Dashboard;

/// <summary>
/// Maps all dashboard REST endpoints onto the WebApplication.
/// Requires PIN auth middleware to be registered (see PinAuthMiddleware).
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        // --- Auth ---
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", Logout);

        // --- Health (no auth) ---
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

        // --- Dashboard data (PIN auth enforced by middleware) ---
        app.MapGet("/api/dashboard", GetDashboardAsync);
        app.MapGet("/api/sessions", GetSessionsAsync);
        app.MapGet("/api/summaries", GetSummariesAsync);
        app.MapGet("/api/events/language", GetLanguageEventsAsync);
        app.MapGet("/api/config", GetConfigAsync);
        app.MapPut("/api/config", PutConfigAsync);
        app.MapGet("/api/reports", GetReportsAsync);

        // --- Fallback: serve SPA for non-API routes ---
        app.MapFallbackToFile("index.html");
    }

    // POST /api/auth/login
    // Body: { "pin": "1234" }
    private static IResult LoginAsync(
        HttpContext context,
        LoginRequest body,
        IOptionsSnapshot<DashboardOptions> options)
    {
        if (body.Pin != options.Value.Pin)
        {
            return Results.Json(new { error = "Invalid PIN." }, statusCode: StatusCodes.Status401Unauthorized);
        }
        PinAuthMiddleware.SetAuthenticated(context);
        return Results.Ok(new { message = "Authenticated." });
    }

    // POST /api/auth/logout
    private static IResult Logout(HttpContext context)
    {
        PinAuthMiddleware.ClearAuthenticated(context);
        return Results.Ok(new { message = "Logged out." });
    }

    // GET /api/dashboard
    // Returns today's screen time summary + recent language detection events.
    private static async Task<IResult> GetDashboardAsync(KidMonitorDbContext db)
    {
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        // Per-app screen time today
        var sessions = await db.AppSessions
            .Where(s => s.StartedAt >= todayStart && s.StartedAt < todayEnd)
            .ToListAsync();

        var perApp = sessions
            .GroupBy(s => s.DisplayName)
            .Select(g => new
            {
                App = g.Key,
                TotalSeconds = g.Sum(s => s.DurationSeconds > 0
                    ? s.DurationSeconds
                    : (int)(DateTime.UtcNow - s.StartedAt).TotalSeconds)
            })
            .OrderByDescending(x => x.TotalSeconds)
            .ToList();

        int totalSeconds = perApp.Sum(x => x.TotalSeconds);

        // Language detection events today
        var langEvents = await db.LanguageDetectionEvents
            .Where(e => e.DetectedAt >= todayStart && e.DetectedAt < todayEnd)
            .OrderByDescending(e => e.DetectedAt)
            .Take(20)
            .Select(e => new
            {
                e.Id,
                e.AppName,
                e.Source,
                e.MatchedTerm,
                e.ContextSnippet,
                e.DetectedAt
            })
            .ToListAsync();

        return Results.Ok(new
        {
            Date = todayStart.ToString("yyyy-MM-dd"),
            TotalScreenTimeSeconds = totalSeconds,
            AppBreakdown = perApp,
            PerApp = perApp,
            FoulLanguageEventCount = langEvents.Count,
            RecentLanguageEvents = langEvents
        });
    }

    // GET /api/sessions?date=YYYY-MM-DD
    private static async Task<IResult> GetSessionsAsync(
        KidMonitorDbContext db,
        string? date)
    {
        DateTime day = date != null && DateTime.TryParse(date, out var parsed)
            ? parsed.Date
            : DateTime.UtcNow.Date;

        var sessions = await db.AppSessions
            .Where(s => s.StartedAt >= day && s.StartedAt < day.AddDays(1))
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.ProcessName,
                s.DisplayName,
                s.StartedAt,
                s.EndedAt,
                DurationSeconds = s.DurationSeconds > 0
                    ? s.DurationSeconds
                    : (s.EndedAt == null ? (int)(DateTime.UtcNow - s.StartedAt).TotalSeconds : 0)
            })
            .ToListAsync();

        return Results.Ok(sessions);
    }

    // GET /api/summaries?days=7
    private static async Task<IResult> GetSummariesAsync(
        KidMonitorDbContext db,
        int? days)
    {
        int lookback = Math.Clamp(days ?? 7, 1, 90);
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-lookback + 1));

        var summaries = await db.DailySummaries
            .Where(s => s.ReportDate >= cutoff)
            .OrderByDescending(s => s.ReportDate)
            .Select(s => new
            {
                s.Id,
                Date = s.ReportDate.ToString(),
                s.TotalScreenTimeSeconds,
                s.FoulLanguageEventCount,
                AppBreakdown = s.AppBreakdownJson,
                s.HtmlReportPath,
                s.GeneratedAt
            })
            .ToListAsync();

        return Results.Ok(summaries);
    }

    // GET /api/events/language?date=YYYY-MM-DD
    private static async Task<IResult> GetLanguageEventsAsync(
        KidMonitorDbContext db,
        string? date)
    {
        DateTime day = date != null && DateTime.TryParse(date, out var parsed)
            ? parsed.Date
            : DateTime.UtcNow.Date;

        var events = await db.LanguageDetectionEvents
            .Where(e => e.DetectedAt >= day && e.DetectedAt < day.AddDays(1))
            .OrderByDescending(e => e.DetectedAt)
            .Select(e => new
            {
                e.Id,
                e.AppName,
                e.Source,
                e.MatchedTerm,
                e.ContextSnippet,
                e.DetectedAt
            })
            .ToListAsync();

        return Results.Ok(events);
    }

    // GET /api/config
    private static IResult GetConfigAsync(IOptionsSnapshot<MonitoringOptions> opts)
    {
        return Results.Ok(opts.Value);
    }

    // PUT /api/config
    // Body: MonitoringOptions JSON — persisted to ProgramData appsettings.json
    private static async Task<IResult> PutConfigAsync(
        MonitoringOptions body,
        IConfiguration configuration,
        ILogger<WebApplication> logger)
    {
        var programDataPath = configuration["Dashboard:ProgramDataPath"] ?? @"C:\ProgramData\KidMonitor";
        var configPath = Path.Combine(programDataPath, "appsettings.json");

        try
        {
            Directory.CreateDirectory(programDataPath);

            // Load existing overrides file (or create new)
            Dictionary<string, object> root;
            if (File.Exists(configPath))
            {
                var existing = await File.ReadAllTextAsync(configPath);
                root = JsonSerializer.Deserialize<Dictionary<string, object>>(existing)
                       ?? new Dictionary<string, object>();
            }
            else
            {
                root = new Dictionary<string, object>();
            }

            // Replace Monitoring section
            root["Monitoring"] = body;

            var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json);

            // Signal the configuration to reload so IOptionsSnapshot picks up new values
            if (configuration is IConfigurationRoot root2)
                root2.Reload();

            return Results.Ok(new { message = "Configuration saved." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write configuration to {Path}", configPath);
            return Results.Problem("Failed to save configuration.", statusCode: 500);
        }
    }

    // GET /api/reports — list past daily HTML reports
    private static async Task<IResult> GetReportsAsync(KidMonitorDbContext db)
    {
        var reports = await db.DailySummaries
            .Where(s => s.HtmlReportPath != null)
            .OrderByDescending(s => s.ReportDate)
            .Take(90)
            .Select(s => new
            {
                Date = s.ReportDate.ToString(),
                Path = s.HtmlReportPath
            })
            .ToListAsync();

        return Results.Ok(reports);
    }
}

/// <summary>Request body for POST /api/auth/login.</summary>
public record LoginRequest(string Pin);
