using System.Net;
using KidMonitor.Tray;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace KidMonitor.Tests.Tray;

/// <summary>
/// Smoke tests for the production tray health poller.
/// </summary>
public sealed class TrayHealthPollTests
{
    private static HttpClient BuildMockClient(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(statusCode));

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

        var exception = await Record.ExceptionAsync(() => poller.CheckAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckAsync_DoesNotThrow_OnTaskCancelledDuringRequest()
    {
        var poller = CreateHealthPoller(BuildThrowingClient(
            new TaskCanceledException("Request timed out")));

        var exception = await Record.ExceptionAsync(() =>
            poller.CheckAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RunAsync_ExitsCleanly_WhenCancellationTokenFired()
    {
        var poller = CreateHealthPoller(
            BuildMockClient(HttpStatusCode.OK),
            pollIntervalMs: 20);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var exception = await Record.ExceptionAsync(() => poller.RunAsync(cts.Token));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RunAsync_ExitsCleanly_WhenCancelledBeforeFirstPoll()
    {
        var poller = CreateHealthPoller(BuildMockClient(HttpStatusCode.OK), pollIntervalMs: 10_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var exception = await Record.ExceptionAsync(() => poller.RunAsync(cts.Token));

        Assert.Null(exception);
    }

    private static HealthPoller CreateHealthPoller(HttpClient client, int pollIntervalMs = 30_000)
        => new(client, NullLogger<HealthPoller>.Instance, TimeSpan.FromMilliseconds(pollIntervalMs));
}
