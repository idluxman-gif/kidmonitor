using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Models;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service.Cloud;

/// <summary>
/// Handles HTTP delivery of monitoring events to the cloud API.
/// </summary>
public sealed class CloudEventPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<CloudApiOptions> options,
    ICloudDeviceCredentialStore credentialStore,
    OfflineCloudEventStore offlineStore,
    ILogger<CloudEventPublisher> logger) : ICloudEventPublisher
{
    public const string HttpClientName = "CloudApi";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly CloudApiOptions _options = options.Value;
    private readonly ICloudDeviceCredentialStore _credentialStore = credentialStore;
    private readonly OfflineCloudEventStore _offlineStore = offlineStore;
    private readonly ILogger<CloudEventPublisher> _logger = logger;

    /// <inheritdoc />
    public async Task PublishAsync(MonitoringEvent monitoringEvent, CancellationToken cancellationToken)
    {
        var flushedAllPending = await FlushPendingCoreAsync(cancellationToken).ConfigureAwait(false);
        if (!flushedAllPending)
        {
            await _offlineStore.BufferAsync(monitoringEvent, cancellationToken).ConfigureAwait(false);
            return;
        }

        var outcome = await SendAsync(monitoringEvent, cancellationToken).ConfigureAwait(false);
        if (outcome is SendOutcome.TransientFailure or SendOutcome.MissingCredentials)
        {
            await _offlineStore.BufferAsync(monitoringEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task FlushPendingAsync(CancellationToken cancellationToken)
    {
        await FlushPendingCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Shared serializer for locally buffered metadata payloads.
    /// </summary>
    public static string SerializeMetadata(IReadOnlyDictionary<string, string?> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);

    /// <summary>
    /// Returns whether an HTTP status should be treated as retryable/offline.
    /// </summary>
    public static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout
        || statusCode == (HttpStatusCode)429
        || (int)statusCode >= 500;

    private async Task<bool> FlushPendingCoreAsync(CancellationToken cancellationToken)
    {
        var credentials = await GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is null)
        {
            return false;
        }

        var pending = await _offlineStore.GetPendingAsync(cancellationToken).ConfigureAwait(false);
        foreach (var bufferedEvent in pending)
        {
            var outcome = await SendAsync(
                ToMonitoringEvent(bufferedEvent),
                credentials,
                cancellationToken).ConfigureAwait(false);
            if (outcome == SendOutcome.Success || outcome == SendOutcome.PermanentFailure)
            {
                await _offlineStore.RemoveAsync(bufferedEvent.Id, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return false;
        }

        return true;
    }

    private async Task<SendOutcome> SendAsync(
        MonitoringEvent monitoringEvent,
        CancellationToken cancellationToken)
    {
        var credentials = await GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is null)
        {
            return SendOutcome.MissingCredentials;
        }

        return await SendAsync(monitoringEvent, credentials, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SendOutcome> SendAsync(
        MonitoringEvent monitoringEvent,
        CloudDeviceCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("Cloud sync skipped because CloudApi:BaseUrl is not configured.");
            return SendOutcome.MissingCredentials;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "events");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.DeviceToken);

            var payload = new CloudEventRequest(
                credentials.DeviceId,
                monitoringEvent.EventType,
                monitoringEvent.Timestamp,
                monitoringEvent.Metadata);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return SendOutcome.Success;
            }

            if (IsTransientStatusCode(response.StatusCode))
            {
                _logger.LogWarning(
                    "Cloud API returned transient status {StatusCode} for event type {EventType}.",
                    (int)response.StatusCode,
                    monitoringEvent.EventType);
                return SendOutcome.TransientFailure;
            }

            _logger.LogError(
                "Cloud API rejected event type {EventType} with status {StatusCode}; dropping event.",
                monitoringEvent.EventType,
                (int)response.StatusCode);
            return SendOutcome.PermanentFailure;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Cloud API unreachable while sending event type {EventType}.", monitoringEvent.EventType);
            return SendOutcome.TransientFailure;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Cloud API timed out while sending event type {EventType}.", monitoringEvent.EventType);
            return SendOutcome.TransientFailure;
        }
    }

    private async Task<CloudDeviceCredentials?> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return null;
        }

        var credentials = await _credentialStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is null
            || string.IsNullOrWhiteSpace(credentials.DeviceToken)
            || string.IsNullOrWhiteSpace(credentials.DeviceId))
        {
            _logger.LogInformation("Cloud sync skipped because the device is not yet paired.");
            return null;
        }

        return credentials;
    }

    private static MonitoringEvent ToMonitoringEvent(PendingCloudEvent pendingCloudEvent)
    {
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            pendingCloudEvent.MetadataJson,
            JsonOptions) ?? [];

        return new MonitoringEvent(
            pendingCloudEvent.EventType,
            pendingCloudEvent.Timestamp,
            metadata);
    }

    private sealed record CloudEventRequest(
        string DeviceId,
        string EventType,
        DateTime Timestamp,
        IReadOnlyDictionary<string, string?> Metadata);

    private enum SendOutcome
    {
        Success,
        TransientFailure,
        PermanentFailure,
        MissingCredentials,
    }
}
