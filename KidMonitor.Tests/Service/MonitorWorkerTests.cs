using KidMonitor.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace KidMonitor.Tests.Service;

/// <summary>
/// Tests for the <see cref="MonitorWorker"/> root orchestrator.
/// In the current implementation <see cref="MonitorWorker"/> is a logging-only stub;
/// tests verify lifecycle correctness only.
/// </summary>
public class MonitorWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_StartsAndStopsWithoutException()
    {
        var worker = new MonitorWorker(NullLogger<MonitorWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ExecuteAsync_CancelsCleanly_WhenTokenPreCancelled()
    {
        var worker = new MonitorWorker(NullLogger<MonitorWorker>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await worker.StartAsync(cts.Token);
        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));

        Assert.Null(ex);
    }
}
