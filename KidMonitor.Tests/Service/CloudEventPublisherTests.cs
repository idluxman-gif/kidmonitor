using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

namespace KidMonitor.Tests.Service;

public sealed class CloudEventPublisherTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KidMonitorDbContext _db;

    public CloudEventPublisherTests()
    {
        _db = InMemoryDbHelper.CreateDb(out _connection);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task PublishAsync_PostsMonitoringEvent_ToCloudApi()
    {
        var handler = new CaptureHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted));
        var publisher = CreatePublisher(handler);

        await publisher.PublishAsync(CreateEvent(), CancellationToken.None);

        Assert.Single(handler.Requests);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://cloud.example/events", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "device-token"), request.Headers.Authorization);

        var body = await request.Content!.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal("device-123", json.RootElement.GetProperty("deviceId").GetString());
        Assert.Equal("foul_language_detected", json.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("WhatsApp Desktop", json.RootElement.GetProperty("metadata").GetProperty("appName").GetString());
        Assert.Empty(await _db.PendingCloudEvents.ToListAsync());
    }

    [Fact]
    public async Task PublishAsync_BuffersEvent_WhenCloudIsUnreachable()
    {
        var handler = new CaptureHttpMessageHandler(_ => throw new HttpRequestException("offline"));
        var publisher = CreatePublisher(handler);

        await publisher.PublishAsync(CreateEvent(), CancellationToken.None);

        var pending = await _db.PendingCloudEvents.ToListAsync();
        Assert.Single(pending);
        Assert.Equal("foul_language_detected", pending[0].EventType);
    }

    [Fact]
    public async Task PublishAsync_BuffersEvent_WhenRateLimited()
    {
        var handler = new CaptureHttpMessageHandler(_ =>
            new HttpResponseMessage((HttpStatusCode)429));
        var publisher = CreatePublisher(handler);

        await publisher.PublishAsync(CreateEvent(), CancellationToken.None);

        Assert.Single(await _db.PendingCloudEvents.ToListAsync());
    }

    [Fact]
    public async Task PublishAsync_DropsEvent_WhenCloudReturnsPermanentClientError()
    {
        var handler = new CaptureHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));
        var publisher = CreatePublisher(handler);

        await publisher.PublishAsync(CreateEvent(), CancellationToken.None);

        Assert.Empty(await _db.PendingCloudEvents.ToListAsync());
    }

    [Fact]
    public async Task FlushPendingAsync_ReplaysBufferedEvents_WhenCloudRecovers()
    {
        var handler = new CaptureHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted));
        var publisher = CreatePublisher(handler);

        await _db.PendingCloudEvents.AddAsync(new PendingCloudEvent
        {
            EventType = "foul_language_detected",
            Timestamp = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["appName"] = "YouTube",
                ["matchedTerm"] = "badword",
            }),
        });
        await _db.SaveChangesAsync();

        await publisher.FlushPendingAsync(CancellationToken.None);

        Assert.Empty(await _db.PendingCloudEvents.ToListAsync());
        Assert.Single(handler.Requests);
    }

    private CloudEventPublisher CreatePublisher(
        CaptureHttpMessageHandler handler,
        CloudApiOptions? options = null,
        Mock<ICloudDeviceCredentialStore>? credentialStore = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cloud.example"),
        };

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(CloudEventPublisher.HttpClientName))
            .Returns(httpClient);

        credentialStore ??= new Mock<ICloudDeviceCredentialStore>();
        credentialStore
            .Setup(store => store.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudDeviceCredentials("device-123", "device-token"));

        var offlineQueue = new OfflineCloudEventStore(
            InMemoryDbHelper.CreateScopeFactory(_db),
            Options.Create(options ?? new CloudApiOptions
            {
                BaseUrl = "https://cloud.example",
                OfflineQueueCapacity = 500,
            }),
            NullLogger<OfflineCloudEventStore>.Instance);

        return new CloudEventPublisher(
            httpClientFactory.Object,
            Options.Create(options ?? new CloudApiOptions
            {
                BaseUrl = "https://cloud.example",
                OfflineQueueCapacity = 500,
            }),
            credentialStore.Object,
            offlineQueue,
            NullLogger<CloudEventPublisher>.Instance);
    }

    private static MonitoringEvent CreateEvent()
    {
        return new MonitoringEvent(
            "foul_language_detected",
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["appName"] = "WhatsApp Desktop",
                ["matchedTerm"] = "badword",
                ["contextSnippet"] = "context with badword",
                ["source"] = "text",
            });
    }

    private sealed class CaptureHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(Clone(request));
            return Task.FromResult(responder(request));
        }

        private static HttpRequestMessage Clone(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                clone.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    request.Content.Headers.ContentType?.MediaType ?? "application/json");

                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
    }
}
