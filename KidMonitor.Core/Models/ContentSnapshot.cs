namespace KidMonitor.Core.Models;

/// <summary>
/// A single captured content event (video title, message text, game chat line).
/// Linked to the ContentSession it belongs to.
/// </summary>
public class ContentSnapshot
{
    public int Id { get; set; }

    public int? ContentSessionId { get; set; }
    public ContentSession? ContentSession { get; set; }

    /// <summary>Friendly app name (e.g. "YouTube", "WhatsApp Desktop", "Discord").</summary>
    public string AppName { get; set; } = string.Empty;

    public ContentType ContentType { get; set; }

    /// <summary>The captured text: video title, visible message fragment, or chat line.</summary>
    public string CapturedText { get; set; } = string.Empty;

    /// <summary>Source URL for video content (YouTube watch URL); null for text content.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Channel / sender name where applicable.</summary>
    public string? Channel { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

public enum ContentType
{
    VideoTitle = 0,
    MessageText = 1,
    GameChat = 2,
}
