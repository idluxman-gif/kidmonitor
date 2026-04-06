using System.Net;
using System.Text;
using System.Text.Json;
using KidMonitor.Service.Cloud;
using Moq;

namespace KidMonitor.Tests.Service;

public sealed class CloudPairingClientTests
{
    [Fact]
    public async Task GenerateAsync_PostsDeviceDetails_AndReturnsThePairingCode()
    {
        var handler = new CaptureHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.Created, new
            {
                pairingCode = "123456",
                qrPayload = "kidmonitor://pair?code=123456",
                expiresAt = "2026-04-04T16:00:00Z",
            }));
        var credentialStore = new Mock<ICloudDeviceCredentialStore>();
        var client = CreateClient(handler, credentialStore);

        var result = await client.GenerateAsync("pc-123", "Kid Desktop", CancellationToken.None);

        Assert.Equal("123456", result.PairingCode);
        Assert.Equal("kidmonitor://pair?code=123456", result.QrPayload);
        Assert.Equal(DateTimeOffset.Parse("2026-04-04T16:00:00Z"), result.ExpiresAt);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://cloud.example/pairing/generate", request.RequestUri!.ToString());

        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("pc-123", body.RootElement.GetProperty("deviceKey").GetString());
        Assert.Equal("Kid Desktop", body.RootElement.GetProperty("deviceName").GetString());
    }

    [Fact]
    public async Task ConfirmAsync_SavesCredentials_WhenThePairingIsConfirmed()
    {
        var credentialStore = new Mock<ICloudDeviceCredentialStore>();
        var handler = new CaptureHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, new
            {
                status = "confirmed",
                deviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                deviceToken = "device-token",
                deviceName = "Kid Desktop",
            }));
        var client = CreateClient(handler, credentialStore);

        var result = await client.ConfirmAsync("pc-123", "123456", CancellationToken.None);

        Assert.Equal(CloudPairingAttemptStatus.Confirmed, result.Status);
        Assert.NotNull(result.Credentials);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", result.Credentials!.DeviceId);
        Assert.Equal("device-token", result.Credentials.DeviceToken);

        credentialStore.Verify(
            store => store.SaveAsync(
                It.Is<CloudDeviceCredentials>(credentials =>
                    credentials.DeviceId == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                    && credentials.DeviceToken == "device-token"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsPending_WhenTheApiHasNotBeenClaimedYet()
    {
        var credentialStore = new Mock<ICloudDeviceCredentialStore>();
        var handler = new CaptureHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.Accepted, new
            {
                status = "pending",
                deviceId = (Guid?)null,
                deviceToken = (string?)null,
                deviceName = (string?)null,
            }));
        var client = CreateClient(handler, credentialStore);

        var result = await client.ConfirmAsync("pc-123", "123456", CancellationToken.None);

        Assert.Equal(CloudPairingAttemptStatus.Pending, result.Status);
        Assert.Null(result.Credentials);
        credentialStore.Verify(
            store => store.SaveAsync(It.IsAny<CloudDeviceCredentials>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsExpired_WhenTheApiRejectsTheCode()
    {
        var credentialStore = new Mock<ICloudDeviceCredentialStore>();
        var handler = new CaptureHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler, credentialStore);

        var result = await client.ConfirmAsync("pc-123", "123456", CancellationToken.None);

        Assert.Equal(CloudPairingAttemptStatus.Expired, result.Status);
        Assert.Null(result.Credentials);
    }

    private static CloudPairingClient CreateClient(
        CaptureHttpMessageHandler handler,
        Mock<ICloudDeviceCredentialStore> credentialStore)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cloud.example"),
        };

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(CloudEventPublisher.HttpClientName))
            .Returns(httpClient);

        return new CloudPairingClient(httpClientFactory.Object, credentialStore.Object);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"),
        };
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
