namespace KidMonitor.Service.Cloud;

/// <summary>
/// Resolves the paired device id/token used for cloud API requests.
/// </summary>
public interface ICloudDeviceCredentialStore
{
    /// <summary>
    /// Loads the currently available cloud credentials.
    /// </summary>
    Task<CloudDeviceCredentials?> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists cloud credentials using the secure local store.
    /// </summary>
    Task SaveAsync(CloudDeviceCredentials credentials, CancellationToken cancellationToken);
}
