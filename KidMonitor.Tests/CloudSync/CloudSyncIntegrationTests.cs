using System.Net;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace KidMonitor.Tests.CloudSync;

/// <summary>
/// Integration tests for the PC-side CloudSyncService (WHA-54).
///
/// These tests use WireMock.Net as a mock cloud API server. Each test starts a real
/// HTTP listener on an ephemeral port, drives the CloudSyncService under test, and
/// asserts the expected interactions.
///
/// STATUS: These tests define the contract that the WHA-54 implementation must satisfy.
///         Tests marked [Fact(Skip=...)] require the CloudSyncService class to exist.
///         Tests marked [Fact] verify the mock-server harness itself and run today.
///
/// Once WHA-54 delivers CloudSyncService, remove all Skip annotations and wire up
/// the real implementation in place of the HttpClient stubs.
/// </summary>
public sealed class CloudSyncIntegrationTests : IDisposable
{
    private readonly WireMockServer _mockApi;

    public CloudSyncIntegrationTests()
    {
        _mockApi = WireMockServer.Start();
    }

    public void Dispose() => _mockApi.Stop();

    // ──────────────────────────────────────────────────────────────────────────
    // Harness smoke test — verifies WireMock.Net is correctly set up
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockServer_CanReceiveAndRespondToEventsPost()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"eventId\":\"test-event-id\"}"));

        using var client = new HttpClient { BaseAddress = new Uri(_mockApi.Url!) };
        var payload = new
        {
            deviceId = "device-abc",
            eventType = "foul_language_detected",
            timestamp = DateTime.UtcNow,
            metadata = new { appName = "WhatsApp", source = "text" }
        };

        var response = await client.PostAsJsonAsync("/events", payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("eventId", body);
    }

    [Fact]
    public async Task MockServer_Returns429_WhenRateLimitStubIsConfigured()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Retry-After", "30"));

        using var client = new HttpClient { BaseAddress = new Uri(_mockApi.Url!) };
        var response = await client.PostAsJsonAsync("/events", new { deviceId = "x" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 1 — Happy path: CloudSyncService posts event successfully
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task Scenario1_HappyPath_PostsEventToCloudApi_Returns202()
    {
        // Arrange
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBody("{\"eventId\":\"abc-123\"}"));

        // TODO (WHA-54): Replace with real CloudSyncService wired to _mockApi.Url
        // var sut = new CloudSyncService(
        //     baseUrl: _mockApi.Url,
        //     deviceToken: "test-device-token",
        //     logger: NullLogger<CloudSyncService>.Instance);
        //
        // var evt = new MonitoringEvent(
        //     DeviceId: "device-001",
        //     EventType: "foul_language_detected",
        //     Timestamp: DateTime.UtcNow,
        //     Metadata: new { AppName = "WhatsApp", Source = "text" });
        //
        // // Act
        // await sut.PostEventAsync(evt);
        //
        // // Assert
        // var calls = _mockApi.LogEntries.Where(e => e.RequestMessage.Path == "/events").ToList();
        // Assert.Single(calls);
        // Assert.Equal("POST", calls[0].RequestMessage.Method);
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 2 — Offline resilience: events buffered when API is unreachable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task Scenario2_OfflineResilience_BuffersEventsWhenApiUnreachable()
    {
        // Arrange: configure mock to simulate unreachable API (connection refused)
        // Stop the mock server to simulate the API being down
        _mockApi.Stop();

        // TODO (WHA-54):
        // var sut = new CloudSyncService(
        //     baseUrl: "http://localhost:19999",  // non-listening port
        //     deviceToken: "test-token",
        //     logger: NullLogger<CloudSyncService>.Instance);
        //
        // var evt = new MonitoringEvent("device-001", "app_opened", DateTime.UtcNow, null);
        //
        // // Act — should not throw; should buffer
        // await sut.PostEventAsync(evt);
        //
        // // Assert
        // Assert.Equal(1, sut.BufferedEventCount);
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task Scenario2_OfflineResilience_FlushesBufferedEventsOnReconnect()
    {
        // Arrange: 3 events buffered while offline
        _mockApi.Stop();

        // TODO (WHA-54):
        // var sut = new CloudSyncService(...);
        // Buffer 3 events while API is down
        // Restart mock API
        // _mockApi = WireMockServer.Start(port: <same port>);
        // _mockApi.Given(Request.Create().WithPath("/events").UsingPost())
        //         .RespondWith(Response.Create().WithStatusCode(202));
        //
        // await sut.FlushBufferedEventsAsync(CancellationToken.None);
        //
        // Assert all 3 were sent
        // var calls = _mockApi.LogEntries.Where(e => e.RequestMessage.Path == "/events").ToList();
        // Assert.Equal(3, calls.Count);
        // Assert.Equal(0, sut.BufferedEventCount);
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 5 — Rate limiting: CloudSyncService backs off on 429
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task Scenario5_RateLimit_BacksOffGracefully_OnHttp429()
    {
        // Arrange: first 60 calls succeed; 61st returns 429
        var callCount = 0;
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithCallback(req =>
                {
                    callCount++;
                    return callCount > 60
                        ? new WireMock.ResponseMessage
                        {
                            StatusCode = 429,
                            Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                            {
                                ["Retry-After"] = new() { "30" }
                            }
                        }
                        : new WireMock.ResponseMessage { StatusCode = 202 };
                }));

        // TODO (WHA-54):
        // var sut = new CloudSyncService(...);
        // Post 65 events
        // Assert: no exception thrown (graceful backoff, not crash)
        // Assert: sut logs contain "Rate limited" or "429" message
        // Assert: after back-off window, posting resumes normally
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Retry policy: 5xx triggers exponential backoff; 4xx does not retry
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task RetryPolicy_5xxError_RetriesUpToThreeTimes()
    {
        // Arrange: first 2 calls return 503, third returns 202
        var callCount = 0;
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithCallback(_ =>
                {
                    callCount++;
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = callCount < 3 ? 503 : 202
                    };
                }));

        // TODO (WHA-54):
        // var sut = new CloudSyncService(..., retryCount: 3, baseDelayMs: 10);
        // await sut.PostEventAsync(evt);
        // Assert.Equal(3, callCount);  // 2 retries + 1 success
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task RetryPolicy_4xxError_DoesNotRetry()
    {
        // Arrange: API returns 401 (invalid device token)
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));

        // TODO (WHA-54):
        // var sut = new CloudSyncService(...);
        // await sut.PostEventAsync(evt);  // should not retry on 4xx
        // Assert: exactly 1 call made (no retries)
        // var calls = _mockApi.LogEntries.Count(e => e.RequestMessage.Path == "/events");
        // Assert.Equal(1, calls);
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Device token: included in Authorization header
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Pending WHA-54: CloudSyncService implementation required")]
    public async Task PostEvent_IncludesDeviceTokenInAuthorizationHeader()
    {
        const string deviceToken = "Bearer test-device-token-xyz";

        _mockApi
            .Given(Request.Create()
                .WithPath("/events")
                .UsingPost()
                .WithHeader("Authorization", deviceToken))
            .RespondWith(Response.Create().WithStatusCode(202));

        // TODO (WHA-54):
        // var sut = new CloudSyncService(baseUrl: _mockApi.Url, deviceToken: deviceToken, ...);
        // await sut.PostEventAsync(evt);
        // Assert: exactly 1 matched call (header assertion is in the stub mapping)
        // var matched = _mockApi.LogEntries.Where(e => e.RequestMessage.Path == "/events").ToList();
        // Assert.Single(matched);
        await Task.CompletedTask;
        throw new SkipException("Pending WHA-54");
    }
}

/// <summary>
/// Thrown inside Skip-annotated tests to surface them as skipped rather than as an error.
/// Replace with xunit.SkipFactAttribute or similar if the project adds that package.
/// </summary>
file sealed class SkipException(string reason) : Exception(reason);
