namespace KidMonitor.Api.Services;

public interface IPushNotificationService
{
    /// <summary>
    /// Sends a push notification to all registered devices for the given parent.
    /// Logs a PushReceipt for each token attempted.
    /// </summary>
    Task SendPushAsync(
        Guid parentId,
        string title,
        string body,
        string notificationType,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);
}
