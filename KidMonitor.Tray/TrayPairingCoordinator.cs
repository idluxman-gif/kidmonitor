using KidMonitor.Service.Cloud;

namespace KidMonitor.Tray;

public sealed class TrayPairingCoordinator(
    ICloudPairingClient pairingClient,
    TimeProvider timeProvider,
    TimeSpan pollInterval)
{
    private readonly ICloudPairingClient _pairingClient = pairingClient;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly TimeSpan _pollInterval = pollInterval < TimeSpan.Zero ? TimeSpan.Zero : pollInterval;

    public async Task<TrayPairingSession> StartAsync(
        TrayDeviceIdentity device,
        CancellationToken cancellationToken)
    {
        var pairingSession = await _pairingClient
            .GenerateAsync(device.DeviceKey, device.DeviceName, cancellationToken)
            .ConfigureAwait(false);

        return new TrayPairingSession(
            device.DeviceKey,
            device.DeviceName,
            pairingSession.PairingCode,
            pairingSession.QrPayload,
            pairingSession.ExpiresAt);
    }

    public async Task<TrayPairingCompletion> WaitForConfirmationAsync(
        TrayPairingSession session,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_timeProvider.GetUtcNow() >= session.ExpiresAt)
            {
                return new TrayPairingCompletion(TrayPairingCompletionStatus.TimedOut, null);
            }

            var result = await _pairingClient
                .ConfirmAsync(session.DeviceKey, session.PairingCode, cancellationToken)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case CloudPairingAttemptStatus.Confirmed:
                    return new TrayPairingCompletion(
                        TrayPairingCompletionStatus.Confirmed,
                        string.IsNullOrWhiteSpace(result.DeviceName) ? session.DeviceName : result.DeviceName);

                case CloudPairingAttemptStatus.Expired:
                    return new TrayPairingCompletion(TrayPairingCompletionStatus.Expired, null);

                case CloudPairingAttemptStatus.Pending:
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected pairing status: {result.Status}.");
            }

            if (_pollInterval > TimeSpan.Zero)
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

public sealed record TrayDeviceIdentity(string DeviceKey, string DeviceName)
{
    public static TrayDeviceIdentity ForCurrentMachine()
    {
        var machineName = Environment.MachineName.Trim();
        return new TrayDeviceIdentity(machineName, $"{machineName} PC");
    }
}

public sealed record TrayPairingSession(
    string DeviceKey,
    string DeviceName,
    string PairingCode,
    string QrPayload,
    DateTimeOffset ExpiresAt);

public sealed record TrayPairingCompletion(
    TrayPairingCompletionStatus Status,
    string? DeviceName);

public enum TrayPairingCompletionStatus
{
    Confirmed,
    Expired,
    TimedOut,
}
