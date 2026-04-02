using KidMonitor.Core.Models;

namespace KidMonitor.Service;

public interface INotificationService
{
    Task SendAppStartedAsync(AppSession session, CancellationToken ct = default);
    Task SendFoulLanguageDetectedAsync(string appName, string snippet, CancellationToken ct = default);
    Task SendDailySummaryAsync(DailySummary summary, CancellationToken ct = default);
}
