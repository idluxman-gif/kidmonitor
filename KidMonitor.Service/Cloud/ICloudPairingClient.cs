namespace KidMonitor.Service.Cloud;

/// <summary>
/// Supports starting and confirming a one-time cloud pairing flow.
/// </summary>
public interface ICloudPairingClient
{
    Task<CloudPairingSession> GenerateAsync(
        string deviceKey,
        string deviceName,
        CancellationToken cancellationToken);

    Task<CloudPairingAttemptResult> ConfirmAsync(
        string deviceKey,
        string pairingCode,
        CancellationToken cancellationToken);
}
