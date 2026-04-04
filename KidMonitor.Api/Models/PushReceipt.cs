namespace KidMonitor.Api.Models;

public class PushReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    public string Platform { get; set; } = string.Empty;           // "fcm" | "apns"
    public string NotificationType { get; set; } = string.Empty;   // "foul_language_alert" | "suspicious_app" | "daily_summary"
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;             // "sent" | "failed"
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Parent Parent { get; set; } = null!;
}
