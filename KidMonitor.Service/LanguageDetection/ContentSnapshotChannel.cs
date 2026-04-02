using KidMonitor.Core.Models;
using System.Threading.Channels;

namespace KidMonitor.Service.LanguageDetection;

/// <summary>
/// In-process channel that bridges <see cref="ContentCapture.ContentCaptureWorker"/>
/// (producer) with <see cref="LanguageDetectionWorker"/> (consumer).
///
/// Registered as a singleton so both workers share the same instance.
/// </summary>
public sealed class ContentSnapshotChannel
{
    private readonly Channel<ContentSnapshot> _channel;

    public ContentSnapshotChannel()
    {
        _channel = Channel.CreateBounded<ContentSnapshot>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelWriter<ContentSnapshot> Writer => _channel.Writer;
    public ChannelReader<ContentSnapshot> Reader => _channel.Reader;
}
