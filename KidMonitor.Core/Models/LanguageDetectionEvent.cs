namespace KidMonitor.Core.Models;

/// <summary>
/// Persisted record of a foul language detection hit.
/// </summary>
public class LanguageDetectionEvent
{
    public int Id { get; set; }

    public int? ContentSessionId { get; set; }
    public ContentSession? ContentSession { get; set; }

    /// <summary>Friendly app name (e.g. "YouTube", "WhatsApp Desktop").</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>Detection source: "text" or "audio".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>The word or phrase that triggered the match (normalised form).</summary>
    public string MatchedTerm { get; set; } = string.Empty;

    /// <summary>Short surrounding text fragment for context.</summary>
    public string ContextSnippet { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
