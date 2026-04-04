using System.Text.Json;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Security;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service.Cloud;

/// <summary>
/// DPAPI-backed device credential store used by the pairing and sync flows.
/// </summary>
public sealed class DpapiCloudDeviceCredentialStore(
    IOptions<CloudApiOptions> options,
    IEncryptionService encryptionService,
    ILogger<DpapiCloudDeviceCredentialStore> logger) : ICloudDeviceCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CloudApiOptions _options = options.Value;
    private readonly IEncryptionService _encryptionService = encryptionService;
    private readonly ILogger<DpapiCloudDeviceCredentialStore> _logger = logger;

    /// <inheritdoc />
    public async Task<CloudDeviceCredentials?> GetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.CredentialsFilePath)
            && File.Exists(_options.CredentialsFilePath))
        {
            try
            {
                var encryptedPayload = await File.ReadAllTextAsync(
                    _options.CredentialsFilePath,
                    cancellationToken).ConfigureAwait(false);
                var decryptedPayload = _encryptionService.Decrypt(encryptedPayload);
                var stored = JsonSerializer.Deserialize<PersistedCredentials>(decryptedPayload, JsonOptions);
                if (stored is not null
                    && !string.IsNullOrWhiteSpace(stored.DeviceId)
                    && !string.IsNullOrWhiteSpace(stored.DeviceToken))
                {
                    return new CloudDeviceCredentials(stored.DeviceId, stored.DeviceToken);
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to read cloud device credentials from {Path}.", _options.CredentialsFilePath);
            }
        }

        if (string.IsNullOrWhiteSpace(_options.DeviceToken))
        {
            return null;
        }

        var deviceId = string.IsNullOrWhiteSpace(_options.DeviceId)
            ? Environment.MachineName
            : _options.DeviceId.Trim();

        return new CloudDeviceCredentials(deviceId, _options.DeviceToken.Trim());
    }

    /// <inheritdoc />
    public async Task SaveAsync(CloudDeviceCredentials credentials, CancellationToken cancellationToken)
    {
        var targetPath = _options.CredentialsFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException("CloudApi:CredentialsFilePath must be configured.");
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = JsonSerializer.Serialize(
            new PersistedCredentials(credentials.DeviceId, credentials.DeviceToken),
            JsonOptions);
        var encryptedPayload = _encryptionService.Encrypt(payload);

        await File.WriteAllTextAsync(targetPath, encryptedPayload, cancellationToken).ConfigureAwait(false);
    }

    private sealed record PersistedCredentials(string DeviceId, string DeviceToken);
}
