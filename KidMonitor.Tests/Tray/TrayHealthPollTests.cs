using System.Net;
using Moq;
using Moq.Protected;

namespace KidMonitor.Tests.Tray;

/// <summary>
/// Smoke tests for the KidMonitor.Tray health-polling component (WHA-29).
///
/// The tray application (KidMonitor.Tray) contains a health poller that calls
/// GET http://localhost:5110/api/health every 30 seconds to determine whether to
/// display "Service: Running" or "Service: Unreachable" in the NotifyIcon context menu.
///
/// These tests validate the polling contract using a mock <see cref="HttpMessageHandler"/>,
/// exercising the logic without starting an actual process or UI.
///
/// SETUP REQUIRED once WHA-29 is complete:
///   1. Add project reference to KidMonitor.Tray in KidMonitor.Tests.csproj.
///   2. Replace the inline interface/stub below with the real HealthPoller class.
///   3. Adjust constructor signature if HealthPoller accepts IHttpClientFactory instead
///      of a raw HttpClient.
///
/// CONTRACT expected from the HealthPoller implementation:
///   - Constructor: HealthPoller(HttpClient httpClient, ILogger&lt;HealthPoller&gt; logger)
///   - Method:      Task&lt;bool&gt; CheckAsync(CancellationToken ct)
///     Returns true  → service is reachable (HTTP 200)
///     Returns false → service is unreachable (non-200, timeout, or network error)
///     Never throws — all exceptions are caught and logged.
///   - Method:      Task RunAsync(CancellationToken ct)
///     Polls in a loop until ct is cancelled. Cancellation exits cleanly (no exception).
/// </summary>
public sealed class TrayHealthPollTests
{
    private const string HealthUrl = "http://localhost:5110/api/health";

    // ── Helper: build a mock HttpClient ─────────────────────────────────────

    private static HttpClient BuildMockClient(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        return new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:5110") };
    }

    private static HttpClient BuildThrowingClient(Exception exception)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        return new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:5110") };
    }

    // ── CheckAsync: reachable service ───────────────────────────────────────

    [Fact]
    public async Task CheckAsync_ReturnsTrue_WhenServiceResponds200()
    {
        var poller = CreateHealthPoller(BuildMockClient(HttpStatusCode.OK));

        var reachable = await poller.CheckAsync(CancellationToken.None);

        Assert.True(reachable, "Expected true when /api/health returns 200");
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CheckAsync_ReturnsFalse_WhenServiceReturnsNon200(HttpStatusCode code)
    {
        var poller = CreateHealthPoller(BuildMockClient(code));

        var reachable = await poller.CheckAsync(CancellationToken.None);

        Assert.False(reachable, $"Expected false when /api/health returns {(int)code}");
    }

    // ── CheckAsync: unreachable service (network errors) ─────────────────────

    [Fact]
    public async Task CheckAsync_ReturnsFalse_WhenConnectionRefused()
    {
        var poller = CreateHealthPoller(BuildThrowingClient(
            new HttpRequestException("Connection refused")));

        var reachable = await poller.CheckAsync(CancellationToken.None);

        Assert.False(reachable, "Expected false on HttpRequestException (connection refused)");
    }

    [Fact]
    public async Task CheckAsync_DoesNotThrow_WhenServiceUnreachable()
    {
        var poller = CreateHealthPoller(BuildThrowingClient(
            new HttpRequestException("Network unreachable")));

        // Must not throw — poller swallows the exception and returns false
        var ex = await Record.ExceptionAsync(() => poller.CheckAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckAsync_DoesNotThrow_OnTaskCancelledDuringRequest()
    {
        var poller = CreateHealthPoller(BuildThrowingClient(
            new TaskCanceledException("Request timed out")));

        var ex = await Record.ExceptionAsync(() =>
            poller.CheckAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    // ── RunAsync: graceful cancellation ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_ExitsCleanly_WhenCancellationTokenFired()
    {
        // Use a fast-polling poller so the loop runs at least once before we cancel
        var poller = CreateHealthPoller(
            BuildMockClient(HttpStatusCode.OK),
            pollIntervalMs: 20);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // RunAsync must return (not throw OperationCanceledException) when ct fires
        var ex = await Record.ExceptionAsync(() => poller.RunAsync(cts.Token));

        Assert.Null(ex);
    }

    [Fact]
    public async Task RunAsync_ExitsCleanly_WhenCancelledBeforeFirstPoll()
    {
        var poller = CreateHealthPoller(BuildMockClient(HttpStatusCode.OK), pollIntervalMs: 10_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        var ex = await Record.ExceptionAsync(() => poller.RunAsync(cts.Token));

        Assert.Null(ex);
    }

    // ── Stub / adapter ───────────────────────────────────────────────────────
    //
    // TODO (WHA-29): replace this stub with the real KidMonitor.Tray.HealthPoller
    // once the KidMonitor.Tray project is created and referenced here.
    // The factory method below must match the real constructor signature.

    private static IHealthPoller CreateHealthPoller(HttpClient client, int pollIntervalMs = 30_000)
        => new StubHealthPoller(client, pollIntervalMs);

    /// <summary>
    /// Inline stub that mirrors the contract described in the class-level summary.
    /// Delete this once the real HealthPoller from KidMonitor.Tray is in scope.
    /// </summary>
    private sealed class StubHealthPoller : IHealthPoller
    {
        private readonly HttpClient _client;
        private readonly int _intervalMs;

        public StubHealthPoller(HttpClient client, int intervalMs)
        {
            _client = client;
            _intervalMs = intervalMs;
        }

        public async Task<bool> CheckAsync(CancellationToken ct)
        {
            try
            {
                using var response = await _client.GetAsync("/api/health", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await CheckAsync(ct);
                try
                {
                    await Task.Delay(_intervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Interface contract for the tray health poller.
    /// KidMonitor.Tray.HealthPoller must implement this interface so these tests
    /// can use the real implementation once WHA-29 is complete.
    /// </summary>
    internal interface IHealthPoller
    {
        /// <summary>Performs a single health check. Returns true if reachable, false otherwise. Never throws.</summary>
        Task<bool> CheckAsync(CancellationToken ct);

        /// <summary>Polls in a loop until <paramref name="ct"/> is cancelled. Exits cleanly on cancellation.</summary>
        Task RunAsync(CancellationToken ct);
    }
}
