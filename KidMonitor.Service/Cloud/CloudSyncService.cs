namespace KidMonitor.Service.Cloud;

/// <summary>
/// Background worker that drains in-memory monitoring events into the cloud publisher.
/// </summary>
public sealed class CloudSyncService(
    MonitoringEventChannel monitoringEventChannel,
    ICloudEventPublisher eventPublisher,
    ILogger<CloudSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleFlushInterval = TimeSpan.FromSeconds(30);

    private readonly MonitoringEventChannel _monitoringEventChannel = monitoringEventChannel;
    private readonly ICloudEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<CloudSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CloudSyncService started.");
        await _eventPublisher.FlushPendingAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            Task<bool> waitToReadTask;
            Task delayTask;

            try
            {
                waitToReadTask = _monitoringEventChannel.Reader.WaitToReadAsync(stoppingToken).AsTask();
                delayTask = Task.Delay(IdleFlushInterval, stoppingToken);
                var completedTask = await Task.WhenAny(waitToReadTask, delayTask).ConfigureAwait(false);

                if (completedTask == delayTask)
                {
                    await _eventPublisher.FlushPendingAsync(stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (!await waitToReadTask.ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            while (_monitoringEventChannel.Reader.TryRead(out var monitoringEvent))
            {
                await _eventPublisher.PublishAsync(monitoringEvent, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
