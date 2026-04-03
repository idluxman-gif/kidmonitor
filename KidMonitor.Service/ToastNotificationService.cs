using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Toolkit.Uwp.Notifications;

namespace KidMonitor.Service;

/// <summary>
/// Sends Windows toast notifications to the parent user session via the
/// Windows Community Toolkit (Microsoft.Toolkit.Uwp.Notifications).
/// Logs every notification to NotificationLog for audit purposes.
/// </summary>
public class ToastNotificationService : INotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ToastNotificationService> _logger;

    public ToastNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<ToastNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SendAppStartedAsync(AppSession session, CancellationToken ct = default)
    {
        var title = "App Opened";
        var body = $"{session.DisplayName} was opened on the monitored PC.";
        await SendAndLogAsync("AppStart", title, body, session.Id, ct);
    }

    public async Task SendFoulLanguageDetectedAsync(string appName, string snippet, CancellationToken ct = default)
    {
        var title = "Foul Language Detected";
        var body = $"Inappropriate content detected in {appName}.";
        await SendAndLogAsync("FoulLanguage", title, body, null, ct);
    }

    public async Task SendDailySummaryAsync(DailySummary summary, CancellationToken ct = default)
    {
        var hours = summary.TotalScreenTimeSeconds / 3600;
        var minutes = (summary.TotalScreenTimeSeconds % 3600) / 60;
        var title = $"Daily Summary — {summary.ReportDate:MMM d}";
        var body = $"Total screen time: {hours}h {minutes}m. Foul language events: {summary.FoulLanguageEventCount}.";
        await SendAndLogAsync("DailySummary", title, body, null, ct);
    }

    public async Task NotifyContentAlertAsync(ContentAlertEvent e, CancellationToken ct = default)
    {
        var title = "Content Alert";
        var body = $"[{e.AppName}] [{e.Source}] {e.Timestamp:HH:mm:ss} - Potential foul language detected.";
        await SendAndLogAsync("ContentAlert", title, body, null, ct);
    }

    private async Task SendAndLogAsync(
        string category, string title, string body,
        int? appSessionId, CancellationToken ct)
    {
        bool delivered = false;
        try
        {
            new ToastContentBuilder()
                .AddAppLogoOverride(null)
                .AddText(title)
                .AddText(body)
                .Show();
            delivered = true;
            _logger.LogInformation("[Toast] {Category}: {Title}", category, title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show toast notification for category {Category}.", category);
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
        db.NotificationLogs.Add(new NotificationLog
        {
            Category = category,
            Title = title,
            Body = body,
            AppSessionId = appSessionId,
            SentAt = DateTime.UtcNow,
            Delivered = delivered
        });
        await db.SaveChangesAsync(ct);
    }
}
