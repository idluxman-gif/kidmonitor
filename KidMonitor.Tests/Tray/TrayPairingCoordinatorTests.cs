using KidMonitor.Service.Cloud;
using KidMonitor.Tray;

namespace KidMonitor.Tests.Tray;

public sealed class TrayPairingCoordinatorTests
{
    [Fact]
    public async Task StartAsync_GeneratesAPairingSessionForTheRequestedDevice()
    {
        var expectedSession = new CloudPairingSession(
            "123456",
            "kidmonitor://pair?code=123456",
            DateTimeOffset.Parse("2026-04-04T18:00:00Z"));
        var client = new FakeCloudPairingClient
        {
            GeneratedSession = expectedSession,
        };
        var coordinator = new TrayPairingCoordinator(client, TimeProvider.System, TimeSpan.Zero);

        var session = await coordinator.StartAsync(
            new TrayDeviceIdentity("pc-123", "Study PC"),
            CancellationToken.None);

        Assert.Equal("pc-123", session.DeviceKey);
        Assert.Equal("Study PC", session.DeviceName);
        Assert.Equal(expectedSession.PairingCode, session.PairingCode);
        Assert.Equal(expectedSession.QrPayload, session.QrPayload);
        Assert.Equal(expectedSession.ExpiresAt, session.ExpiresAt);

        var request = Assert.Single(client.GenerateRequests);
        Assert.Equal("pc-123", request.DeviceKey);
        Assert.Equal("Study PC", request.DeviceName);
    }

    [Fact]
    public async Task WaitForConfirmationAsync_ReturnsConfirmedAfterPendingPolls()
    {
        var client = new FakeCloudPairingClient();
        client.ConfirmResults.Enqueue(new CloudPairingAttemptResult(CloudPairingAttemptStatus.Pending, null, null));
        client.ConfirmResults.Enqueue(new CloudPairingAttemptResult(
            CloudPairingAttemptStatus.Confirmed,
            new CloudDeviceCredentials("device-1", "device-token"),
            "Study PC"));

        var coordinator = new TrayPairingCoordinator(client, TimeProvider.System, TimeSpan.Zero);
        var session = new TrayPairingSession(
            "pc-123",
            "Study PC",
            "123456",
            "kidmonitor://pair?code=123456",
            DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await coordinator.WaitForConfirmationAsync(session, CancellationToken.None);

        Assert.Equal(TrayPairingCompletionStatus.Confirmed, result.Status);
        Assert.Equal("Study PC", result.DeviceName);
        Assert.Collection(
            client.ConfirmRequests,
            request =>
            {
                Assert.Equal("pc-123", request.DeviceKey);
                Assert.Equal("123456", request.PairingCode);
            },
            request =>
            {
                Assert.Equal("pc-123", request.DeviceKey);
                Assert.Equal("123456", request.PairingCode);
            });
    }

    [Fact]
    public async Task WaitForConfirmationAsync_ReturnsExpiredWhenTheCloudRejectsTheCode()
    {
        var client = new FakeCloudPairingClient();
        client.ConfirmResults.Enqueue(new CloudPairingAttemptResult(CloudPairingAttemptStatus.Expired, null, null));

        var coordinator = new TrayPairingCoordinator(client, TimeProvider.System, TimeSpan.Zero);
        var session = new TrayPairingSession(
            "pc-123",
            "Study PC",
            "123456",
            "kidmonitor://pair?code=123456",
            DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await coordinator.WaitForConfirmationAsync(session, CancellationToken.None);

        Assert.Equal(TrayPairingCompletionStatus.Expired, result.Status);
        Assert.Null(result.DeviceName);
        Assert.Single(client.ConfirmRequests);
    }

    [Fact]
    public async Task WaitForConfirmationAsync_ReturnsTimedOutWithoutPollingWhenSessionAlreadyExpired()
    {
        var client = new FakeCloudPairingClient();
        var coordinator = new TrayPairingCoordinator(
            client,
            new FixedTimeProvider(DateTimeOffset.Parse("2026-04-04T18:00:00Z")),
            TimeSpan.Zero);
        var session = new TrayPairingSession(
            "pc-123",
            "Study PC",
            "123456",
            "kidmonitor://pair?code=123456",
            DateTimeOffset.Parse("2026-04-04T17:59:59Z"));

        var result = await coordinator.WaitForConfirmationAsync(session, CancellationToken.None);

        Assert.Equal(TrayPairingCompletionStatus.TimedOut, result.Status);
        Assert.Empty(client.ConfirmRequests);
    }

    private sealed class FakeCloudPairingClient : ICloudPairingClient
    {
        public List<(string DeviceKey, string DeviceName)> GenerateRequests { get; } = [];

        public Queue<CloudPairingAttemptResult> ConfirmResults { get; } = new();

        public List<(string DeviceKey, string PairingCode)> ConfirmRequests { get; } = [];

        public CloudPairingSession? GeneratedSession { get; set; }

        public Task<CloudPairingSession> GenerateAsync(
            string deviceKey,
            string deviceName,
            CancellationToken cancellationToken)
        {
            GenerateRequests.Add((deviceKey, deviceName));
            return Task.FromResult(
                GeneratedSession
                ?? new CloudPairingSession(
                    "123456",
                    "kidmonitor://pair?code=123456",
                    DateTimeOffset.UtcNow.AddMinutes(10)));
        }

        public Task<CloudPairingAttemptResult> ConfirmAsync(
            string deviceKey,
            string pairingCode,
            CancellationToken cancellationToken)
        {
            ConfirmRequests.Add((deviceKey, pairingCode));
            if (ConfirmResults.Count == 0)
            {
                throw new InvalidOperationException("No queued confirm result.");
            }

            return Task.FromResult(ConfirmResults.Dequeue());
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
