using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace KidMonitor.Service.Cloud;

/// <summary>
/// Talks to the cloud pairing endpoints and persists confirmed credentials locally.
/// </summary>
public sealed class CloudPairingClient(
    IHttpClientFactory httpClientFactory,
    ICloudDeviceCredentialStore credentialStore) : ICloudPairingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ICloudDeviceCredentialStore _credentialStore = credentialStore;

    public async Task<CloudPairingSession> GenerateAsync(
        string deviceKey,
        string deviceName,
        CancellationToken cancellationToken)
    {
        var normalizedDeviceKey = NormalizeRequired(deviceKey, nameof(deviceKey));
        var normalizedDeviceName = NormalizeRequired(deviceName, nameof(deviceName));

        var client = _httpClientFactory.CreateClient(CloudEventPublisher.HttpClientName);
        using var response = await client.PostAsJsonAsync(
            "pairing/generate",
            new GeneratePairingRequest(normalizedDeviceKey, normalizedDeviceName),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var pairingSession = await response.Content.ReadFromJsonAsync<CloudPairingSession>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return pairingSession ?? throw new InvalidOperationException("Cloud pairing response was empty.");
    }

    public async Task<CloudPairingAttemptResult> ConfirmAsync(
        string deviceKey,
        string pairingCode,
        CancellationToken cancellationToken)
    {
        var normalizedDeviceKey = NormalizeRequired(deviceKey, nameof(deviceKey));
        var normalizedPairingCode = NormalizeRequired(pairingCode, nameof(pairingCode));

        var client = _httpClientFactory.CreateClient(CloudEventPublisher.HttpClientName);
        using var response = await client.PostAsJsonAsync(
            "pairing/confirm",
            new ConfirmPairingRequest(normalizedDeviceKey, normalizedPairingCode),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new CloudPairingAttemptResult(CloudPairingAttemptStatus.Pending, null, null);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CloudPairingAttemptResult(CloudPairingAttemptStatus.Expired, null, null);
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ConfirmPairingResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cloud pairing confirmation response was empty.");

        if (!string.Equals(payload.Status, "confirmed", StringComparison.OrdinalIgnoreCase)
            || payload.DeviceId is null
            || string.IsNullOrWhiteSpace(payload.DeviceToken))
        {
            throw new InvalidOperationException("Cloud pairing confirmation response was invalid.");
        }

        var credentials = new CloudDeviceCredentials(payload.DeviceId.Value.ToString(), payload.DeviceToken);
        await _credentialStore.SaveAsync(credentials, cancellationToken).ConfigureAwait(false);

        return new CloudPairingAttemptResult(
            CloudPairingAttemptStatus.Confirmed,
            credentials,
            payload.DeviceName);
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return normalized;
    }

    private sealed record GeneratePairingRequest(string DeviceKey, string DeviceName);

    private sealed record ConfirmPairingRequest(string DeviceKey, string PairingCode);

    private sealed record ConfirmPairingResponse(
        string Status,
        Guid? DeviceId,
        string? DeviceToken,
        string? DeviceName);
}

public sealed record CloudPairingSession(string PairingCode, string QrPayload, DateTimeOffset ExpiresAt);

public sealed record CloudPairingAttemptResult(
    CloudPairingAttemptStatus Status,
    CloudDeviceCredentials? Credentials,
    string? DeviceName);

public enum CloudPairingAttemptStatus
{
    Pending,
    Confirmed,
    Expired,
}
