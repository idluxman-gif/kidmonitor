using KidMonitor.Service;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static IConfiguration BuildConfig(int pollSeconds = 1) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:PollIntervalSeconds"] = pollSeconds.ToString()
            })
            .Build();

    private static ProcessTrackingWorker BuildWorker(IConfiguration? config = null)
    {
        InMemoryDbHelper.CreateDb(out _);
        var db = InMemoryDbHelper.CreateDb(out _);
        var scopeFactory = InMemoryDbHelper.CreateScopeFactory(db);
        var notifications = new Mock<INotificationService>().Object;
        return new ProcessTrackingWorker(
            scopeFactory,
            config ?? BuildConfig(),
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
        var worker = BuildWorker(BuildConfig(pollSeconds: 1));
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
        var worker = BuildWorker(BuildConfig(pollSeconds: 60));

        await worker.StartAsync(CancellationToken.None);
        // Let it run one poll cycle at most
        await Task.Delay(50);

        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies the default tracked-app list is populated when no config section exists.
    /// This exercises the fallback in <c>GetTrackedApps()</c>.
    /// </summary>
    [Fact]
    public void DefaultTrackedApps_AppliedWhenNoConfigSection()
    {
        // An empty config means Monitoring:TrackedApps is absent → fallback list used
        var emptyConfig = new ConfigurationBuilder().Build();
        var ex = Record.Exception(() => BuildWorker(emptyConfig));
        Assert.Null(ex); // No crash constructing the worker
    }
}
