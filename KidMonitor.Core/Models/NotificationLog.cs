namespace KidMonitor.Core.Models;

/// <summary>
/// Records every toast notification sent to the parent.
/// </summary>
public class NotificationLog
{
    public int Id { get; set; }

    /// <summary>Category: "AppStart", "FoulLanguage", "TimeLimit", etc.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Short notification title shown in the toast.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full notification body text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional reference to the AppSession that triggered this notification.</summary>
    public int? AppSessionId { get; set; }
    public AppSession? AppSession { get; set; }

    /// <summary>UTC timestamp when the notification was sent.</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the notification was successfully displayed.</summary>
    public bool Delivered { get; set; }
}
