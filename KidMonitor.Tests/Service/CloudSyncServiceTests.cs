using KidMonitor.Service.Cloud;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KidMonitor.Tests.Service;

public sealed class CloudSyncServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesQueuedMonitoringEvents()
    {
        var channel = new MonitoringEventChannel();
        var publisher = new Mock<ICloudEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<MonitoringEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisher
            .Setup(p => p.FlushPendingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new CloudSyncService(
            channel,
            publisher.Object,
            NullLogger<CloudSyncService>.Instance);

        await worker.StartAsync(CancellationToken.None);

        channel.Writer.TryWrite(new MonitoringEvent(
            "foul_language_detected",
            DateTime.UtcNow,
            new Dictionary<string, string?>()));

        await Task.Delay(250, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<MonitoringEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
