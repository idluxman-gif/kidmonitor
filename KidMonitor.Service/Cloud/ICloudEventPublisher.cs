namespace KidMonitor.Service.Cloud;

/// <summary>
/// Publishes monitoring events to the cloud API and replays buffered events when possible.
/// </summary>
public interface ICloudEventPublisher
{
    /// <summary>
    /// Publishes a new monitoring event, buffering it locally when delivery is not currently possible.
    /// </summary>
    Task PublishAsync(MonitoringEvent monitoringEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to flush any locally buffered monitoring events.
    /// </summary>
    Task FlushPendingAsync(CancellationToken cancellationToken);
}
