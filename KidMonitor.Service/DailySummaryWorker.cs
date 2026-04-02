using System.Text;
using System.Text.Json;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service;

/// <summary>
/// Generates a daily HTML usage report at a configured local time and sends a toast summary.
/// Runs once per day; on service restart it checks if today's report has already been generated.
/// </summary>
public class DailySummaryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotificationOptions> _notificationOptions;
    private readonly IOptions<DatabaseOptions> _databaseOptions;
    private readonly ILogger<DailySummaryWorker> _logger;
    private readonly INotificationService _notifications;

    public DailySummaryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOptions> notificationOptions,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<DailySummaryWorker> logger,
        INotificationService notifications)
    {
        _scopeFactory = scopeFactory;
        _notificationOptions = notificationOptions;
        _databaseOptions = databaseOptions;
        _logger = logger;
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var summaryTime = TimeOnly.Parse(_notificationOptions.Value.DailySummaryTimeLocal);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var todaySummaryAt = today.ToDateTime(summaryTime);

            if (now < todaySummaryAt)
            {
                // Wait until scheduled time today
                var delay = todaySummaryAt - now;
                _logger.LogInformation("Daily summary scheduled in {Minutes:N0} minutes.", delay.TotalMinutes);
                await Task.Delay(delay, stoppingToken);
            }
            else
            {
                // Check if we already ran today
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
                var existing = await db.DailySummaries.FirstOrDefaultAsync(d => d.ReportDate == today, stoppingToken);
                if (existing is null)
                {
                    var summary = await GenerateSummaryAsync(db, today, stoppingToken);
                    await _notifications.SendDailySummaryAsync(summary, stoppingToken);
                }
                // Wait until the scheduled time tomorrow (exact duration, avoids drift on restart)
                var tomorrow = today.AddDays(1);
                var delay = tomorrow.ToDateTime(summaryTime) - DateTime.Now;
                _logger.LogInformation("Next daily summary in {Minutes:N0} minutes.", delay.TotalMinutes);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task<DailySummary> GenerateSummaryAsync(
        KidMonitorDbContext db, DateOnly date, CancellationToken ct)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var endUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local).ToUniversalTime();

        var sessions = await db.AppSessions
            .Where(s => s.StartedAt >= startUtc && s.StartedAt <= endUtc)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var totalSeconds = sessions.Sum(s =>
            s.EndedAt == null ? (int)(now - s.StartedAt).TotalSeconds : s.DurationSeconds);
        var breakdown = sessions
            .GroupBy(s => s.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(s =>
                s.EndedAt == null ? (int)(now - s.StartedAt).TotalSeconds : s.DurationSeconds));

        var detectionEvents = await db.LanguageDetectionEvents
            .Where(e => e.DetectedAt >= startUtc && e.DetectedAt <= endUtc)
            .ToListAsync(ct);

        var foulCount = detectionEvents.Count;
        var foulByApp = detectionEvents
            .GroupBy(e => e.AppName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count());
        var youtubeSnippets = detectionEvents
            .Where(e => e.AppName.Contains("youtube", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ContextSnippet)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        var reportDir = Path.Combine(
            Path.GetDirectoryName(_databaseOptions.Value.Path) ?? ".", "reports");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, $"summary-{date:yyyy-MM-dd}.html");

        var html = BuildHtml(date, totalSeconds, breakdown, foulCount, foulByApp, youtubeSnippets);
        await File.WriteAllTextAsync(reportPath, html, Encoding.UTF8, ct);

        var summary = new DailySummary
        {
            ReportDate = date,
            TotalScreenTimeSeconds = totalSeconds,
            AppBreakdownJson = JsonSerializer.Serialize(breakdown),
            FoulLanguageEventCount = foulCount,
            HtmlReportPath = reportPath,
            GeneratedAt = DateTime.UtcNow
        };
        db.DailySummaries.Add(summary);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Daily summary generated for {Date}: {Seconds}s total.", date, totalSeconds);
        return summary;
    }

    private static string BuildHtml(
        DateOnly date, int totalSeconds,
        Dictionary<string, int> breakdown, int foulCount,
        Dictionary<string, int>? foulByApp = null,
        List<string>? youtubeSnippets = null)
    {
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine($"<title>KidMonitor — {date:MMMM d, yyyy}</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;max-width:600px;margin:2rem auto;color:#222}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}td,th{padding:.5rem 1rem;border:1px solid #ddd}");
        sb.AppendLine("th{background:#f0f4ff}.warn{color:#c00}</style></head><body>");
        sb.AppendLine($"<h1>Daily Usage Report</h1><p><strong>Date:</strong> {date:MMMM d, yyyy}</p>");
        sb.AppendLine($"<p><strong>Total screen time:</strong> {hours}h {minutes}m</p>");
        if (foulCount > 0)
            sb.AppendLine($"<p class=\"warn\"><strong>Foul language events:</strong> {foulCount}</p>");
        sb.AppendLine("<h2>App Breakdown</h2><table><tr><th>App</th><th>Time</th></tr>");
        foreach (var (app, secs) in breakdown.OrderByDescending(x => x.Value))
        {
            var h = secs / 3600; var m = (secs % 3600) / 60;
            sb.AppendLine($"<tr><td>{app}</td><td>{h}h {m}m</td></tr>");
        }
        sb.AppendLine("</table>");

        if (foulCount > 0)
        {
            sb.AppendLine("<h2>Content Monitoring</h2>");
            sb.AppendLine($"<p class=\"warn\">Total foul language detections: {foulCount}</p>");

            if (foulByApp is { Count: > 0 })
            {
                sb.AppendLine("<table><tr><th>App</th><th>Detections</th></tr>");
                foreach (var (app, count) in foulByApp.OrderByDescending(x => x.Value))
                    sb.AppendLine($"<tr><td>{app}</td><td>{count}</td></tr>");
                sb.AppendLine("</table>");
            }

            if (youtubeSnippets is { Count: > 0 })
            {
                sb.AppendLine("<h3>YouTube Context Snippets</h3><ul>");
                foreach (var snippet in youtubeSnippets)
                    sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(snippet)}</li>");
                sb.AppendLine("</ul>");
            }
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
