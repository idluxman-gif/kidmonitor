using System.Threading.Channels;

namespace KidMonitor.Service.Cloud;

/// <summary>
/// In-process channel that bridges local detectors with <see cref="CloudSyncService"/>.
/// </summary>
public sealed class MonitoringEventChannel
{
    private readonly Channel<MonitoringEvent> _channel = Channel.CreateUnbounded<MonitoringEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelReader<MonitoringEvent> Reader => _channel.Reader;

    public ChannelWriter<MonitoringEvent> Writer => _channel.Writer;
}
