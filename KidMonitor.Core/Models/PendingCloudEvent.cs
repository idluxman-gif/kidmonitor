namespace KidMonitor.Core.Models;

/// <summary>
/// Locally buffered monitoring event waiting to be replayed to the cloud API.
/// </summary>
public class PendingCloudEvent
{
    public int Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTime Timestamp { get; set; }

    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}
