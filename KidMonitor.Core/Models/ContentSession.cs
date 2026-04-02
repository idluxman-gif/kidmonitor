namespace KidMonitor.Core.Models;

/// <summary>
/// Tracks a contiguous session of consuming a specific piece of content
/// (e.g. watching a YouTube video, an active WhatsApp conversation, a game chat session).
/// </summary>
public class ContentSession
{
    public int Id { get; set; }

    /// <summary>The process session running the content app; null if not separately tracked.</summary>
    public int? AppSessionId { get; set; }
    public AppSession? AppSession { get; set; }

    /// <summary>Friendly app name (e.g. "YouTube", "WhatsApp Desktop", "Discord").</summary>
    public string AppName { get; set; } = string.Empty;

    public ContentType ContentType { get; set; }

    /// <summary>Title of the content (video title, contact name, game name).</summary>
    public string ContentTitle { get; set; } = string.Empty;

    /// <summary>Stable identifier, e.g. YouTube watch URL or game process name.</summary>
    public string? ContentIdentifier { get; set; }

    /// <summary>Channel / sender / game name where applicable.</summary>
    public string? Channel { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Total duration in seconds (computed on session close).</summary>
    public int DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ContentSnapshot> Snapshots { get; set; } = new List<ContentSnapshot>();
}
