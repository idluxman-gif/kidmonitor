using System.Text.Json;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using KidMonitor.Service.Cloud;
using KidMonitor.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace KidMonitor.Tests.CloudSync;

/// <summary>
/// Integration tests for the cloud sync pipeline (WHA-58).
///
/// These tests drive <see cref="CloudEventPublisher"/> against a real WireMock.Net HTTP server
/// to validate the full HTTP request/response path for all 6 E2E test scenarios.
/// A real in-process SQLite database is used for the offline event buffer so that
/// buffering and flush behaviour is exercised end-to-end.
///
/// Why CloudEventPublisher and not CloudSyncService?
///   CloudSyncService is a thin background drain loop — its unit tests live in
///   CloudSyncServiceTests.cs. The component that owns all cloud-protocol decisions
///   (auth header, retry classification, offline buffering) is CloudEventPublisher.
///   Testing it against a live HTTP server gives the highest confidence that the
///   real wire format is correct.
/// </summary>
public sealed class CloudSyncIntegrationTests : IDisposable
{
    private const string DeviceId = "device-001";
    private const string DeviceToken = "test-device-token";

    private readonly WireMockServer _mockApi;
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;
    private readonly CloudEventPublisher _publisher;

    public CloudSyncIntegrationTests()
    {
        _mockApi = WireMockServer.Start();
        _db = InMemoryDbHelper.CreateDb(out _connection);
        _publisher = BuildPublisher(_mockApi.Url!);
    }

    public void Dispose()
    {
        try { _mockApi.Stop(); } catch { /* already stopped by a test */ }
        _db.Dispose();
        _connection.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 1 — Happy path
    // PC service posts event → cloud API returns 202 → correct JSON body +
    // Authorization header sent → nothing buffered locally.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Scenario1_HappyPath_PostsEventToCloudApi_Returns202()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"eventId\":\"abc-123\"}"));

        await _publisher.PublishAsync(CreateEvent("foul_language_detected"), CancellationToken.None);

        var calls = _mockApi.LogEntries
            .Where(e => e.RequestMessage.Path == "/events")
            .ToList();

        Assert.Single(calls);

        var call = calls[0];
        Assert.Equal("POST", call.RequestMessage.Method.ToUpperInvariant());
        Assert.Contains($"Bearer {DeviceToken}", call.RequestMessage.Headers!["Authorization"].First());

        using var json = JsonDocument.Parse(call.RequestMessage.Body!);
        Assert.Equal(DeviceId, json.RootElement.GetProperty("deviceId").GetString());
        Assert.Equal("foul_language_detected", json.RootElement.GetProperty("eventType").GetString());

        Assert.Empty(await _db.PendingCloudEvents.ToListAsync());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 2a — Offline resilience: API unreachable → event buffered locally
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Scenario2_OfflineResilience_BuffersEventsWhenApiUnreachable()
    {
        // Simulate the cloud API going down
        _mockApi.Stop();

        await _publisher.PublishAsync(CreateEvent("app_opened"), CancellationToken.None);

        var pending = await _db.PendingCloudEvents.ToListAsync();
        Assert.Single(pending);
        Assert.Equal("app_opened", pending[0].EventType);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 2b — Offline resilience: API recovers → all buffered events flushed
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Scenario2_OfflineResilience_FlushesBufferedEventsOnReconnect()
    {
        // Pre-seed 3 buffered events that accumulated while the API was down
        for (var i = 0; i < 3; i++)
        {
            _db.PendingCloudEvents.Add(new PendingCloudEvent
            {
                EventType = "foul_language_detected",
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                MetadataJson = JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["appName"] = $"App{i}",
                    ["matchedTerm"] = "badword",
                }),
                EnqueuedAt = DateTime.UtcNow.AddMinutes(-i),
            });
        }
        await _db.SaveChangesAsync();

        // API is back — stub all POST /events to return 202
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        await _publisher.FlushPendingAsync(CancellationToken.None);

        // All 3 events sent and removed from the buffer
        Assert.Empty(await _db.PendingCloudEvents.ToListAsync());
        var calls = _mockApi.LogEntries.Where(e => e.RequestMessage.Path == "/events").ToList();
        Assert.Equal(3, calls.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 5 — Rate limiting: 429 treated as transient → event buffered
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Scenario5_RateLimit_BuffersEventGracefully_OnHttp429()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Retry-After", "30"));

        // Must not throw; 429 is transient so the event should be buffered for later replay
        await _publisher.PublishAsync(CreateEvent("foul_language_detected"), CancellationToken.None);

        var pending = await _db.PendingCloudEvents.ToListAsync();
        Assert.Single(pending);
        Assert.Equal("foul_language_detected", pending[0].EventType);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Retry policy — 5xx transient: event buffered, no exception propagated
    // Note: CloudEventPublisher classifies 5xx as transient and buffers rather
    // than retrying inline; retry happens on the next FlushPendingAsync cycle.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryPolicy_5xxError_BuffersEventAndDoesNotThrow()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        await _publisher.PublishAsync(CreateEvent("foul_language_detected"), CancellationToken.None);

        Assert.Single(await _db.PendingCloudEvents.ToListAsync());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Retry policy — 4xx permanent: event dropped, exactly 1 HTTP call, no buffer
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryPolicy_4xxError_DropsEvent_NoBufferNoRetry()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));

        await _publisher.PublishAsync(CreateEvent("foul_language_detected"), CancellationToken.None);

        // 4xx is a permanent failure: event must be dropped (not buffered)
        Assert.Empty(await _db.PendingCloudEvents.ToListAsync());

        // Must not retry — exactly 1 HTTP attempt
        var calls = _mockApi.LogEntries.Where(e => e.RequestMessage.Path == "/events").ToList();
        Assert.Single(calls);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Device token: Authorization: Bearer header included on every outbound request
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostEvent_IncludesDeviceTokenInAuthorizationHeader()
    {
        _mockApi
            .Given(Request.Create().WithPath("/events").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        await _publisher.PublishAsync(CreateEvent("foul_language_detected"), CancellationToken.None);

        var calls = _mockApi.LogEntries.Where(e => e.RequestMessage.Path == "/events").ToList();
        Assert.Single(calls);
        Assert.Contains($"Bearer {DeviceToken}", calls[0].RequestMessage.Headers!["Authorization"].First());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private CloudEventPublisher BuildPublisher(string baseUrl)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(CloudEventPublisher.HttpClientName))
            .Returns(httpClient);

        var credentialStore = new Mock<ICloudDeviceCredentialStore>();
        credentialStore
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudDeviceCredentials(DeviceId, DeviceToken));

        var options = Options.Create(new CloudApiOptions
        {
            BaseUrl = baseUrl,
            OfflineQueueCapacity = 500,
        });

        var offlineStore = new OfflineCloudEventStore(
            InMemoryDbHelper.CreateScopeFactory(_db),
            options,
            NullLogger<OfflineCloudEventStore>.Instance);

        return new CloudEventPublisher(
            httpClientFactory.Object,
            options,
            credentialStore.Object,
            offlineStore,
            NullLogger<CloudEventPublisher>.Instance);
    }

    private static MonitoringEvent CreateEvent(string eventType) =>
        new(eventType, DateTime.UtcNow, new Dictionary<string, string?>
        {
            ["appName"] = "WhatsApp Desktop",
            ["matchedTerm"] = "badword",
        });
}
