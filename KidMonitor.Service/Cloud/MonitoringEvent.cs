namespace KidMonitor.Service.Cloud;

/// <summary>
/// Well-known cloud event types emitted by the Windows service.
/// </summary>
public static class MonitoringEventTypes
{
    public const string FoulLanguageDetected = "foul_language_detected";
}

/// <summary>
/// In-memory event payload passed from local detectors to the cloud sync worker.
/// </summary>
/// <param name="EventType">Cloud event type identifier.</param>
/// <param name="Timestamp">UTC timestamp when the event occurred.</param>
/// <param name="Metadata">Event metadata serialized into the cloud request payload.</param>
public sealed record MonitoringEvent(
    string EventType,
    DateTime Timestamp,
    IReadOnlyDictionary<string, string?> Metadata);

/// <summary>
/// Securely stored credentials used to authenticate cloud event uploads.
/// </summary>
/// <param name="DeviceId">Cloud-side device identifier.</param>
/// <param name="DeviceToken">Bearer token issued during pairing.</param>
public sealed record CloudDeviceCredentials(string DeviceId, string DeviceToken);
