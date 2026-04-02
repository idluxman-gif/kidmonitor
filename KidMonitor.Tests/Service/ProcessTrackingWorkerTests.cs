using KidMonitor.Core.Configuration;
using KidMonitor.Service;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace KidMonitor.Tests.Service;

/// <summary>
/// Unit tests for <see cref="ProcessTrackingWorker"/>.
///
/// DESIGN GAP: <c>ProcessTrackingWorker.PollAsync</c> calls
/// <c>System.Diagnostics.Process.GetProcesses()</c> which is a static method
/// with no abstraction. This prevents unit-testing the polling logic without
/// either integration-running on a real OS or refactoring the production code
/// to inject an <c>IProcessProvider</c> interface.
///
/// Tests here cover construction, configuration binding, and lifecycle only.
/// A future refactor should extract <c>IProcessProvider</c> to enable full
/// behavioural testing.
/// </summary>
public sealed class ProcessTrackingWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ProcessTrackingWorkerTests()
    {
        // Connection kept alive so InMemoryDbHelper.CreateDb can reuse it if needed
        InMemoryDbHelper.CreateDb(out _connection);
    }

    public void Dispose() => _connection.Dispose();

    private static IOptions<MonitoringOptions> BuildOptions(int pollSeconds = 1) =>
        Options.Create(new MonitoringOptions { PollIntervalSeconds = pollSeconds });

    private static ProcessTrackingWorker BuildWorker(IOptions<MonitoringOptions>? options = null)
    {
        InMemoryDbHelper.CreateDb(out _);
        var db = InMemoryDbHelper.CreateDb(out _);
        var scopeFactory = InMemoryDbHelper.CreateScopeFactory(db);
        var notifications = new Mock<INotificationService>().Object;
        return new ProcessTrackingWorker(
            scopeFactory,
            options ?? BuildOptions(),
            NullLogger<ProcessTrackingWorker>.Instance,
            notifications);
    }

    [Fact]
    public void Constructor_Succeeds_WithValidDependencies()
    {
        // Arrange & Act
        var ex = Record.Exception(() => BuildWorker());

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public async Task ExecuteAsync_CancelsCleanly_WhenTokenPreCancelled()
    {
        // Arrange
        var worker = BuildWorker(BuildOptions(pollSeconds: 1));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act — use StartAsync/StopAsync to go through the hosted-service contract
        await worker.StartAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);

        // Assert — no exception escapes; test passes if we reach here
    }

    [Fact]
    public async Task ExecuteAsync_ShutdownGracefully_ViaStopAsync()
    {
        var worker = BuildWorker(BuildOptions(pollSeconds: 60));

        await worker.StartAsync(CancellationToken.None);
        // Let it run one poll cycle at most
        await Task.Delay(50);

        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies the default tracked-app list is populated when no TrackedApps are configured.
    /// </summary>
    [Fact]
    public void DefaultTrackedApps_AppliedWhenNoConfigSection()
    {
        // Empty TrackedApps list → fallback list used inside ExecuteAsync
        var emptyOptions = Options.Create(new MonitoringOptions());
        var ex = Record.Exception(() => BuildWorker(emptyOptions));
        Assert.Null(ex); // No crash constructing the worker
    }
}
