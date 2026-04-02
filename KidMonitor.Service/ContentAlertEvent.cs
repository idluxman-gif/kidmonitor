namespace KidMonitor.Service;

/// <summary>
/// Payload for a content-level foul language alert notification.
/// ContextSnippet must be pre-sanitised (no raw slurs) before constructing this record.
/// </summary>
public sealed record ContentAlertEvent(
    string AppName,
    DateTime Timestamp,
    string ContextSnippet,
    string Source   // "text" or "audio"
);
