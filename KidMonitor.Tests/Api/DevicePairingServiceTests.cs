using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using KidMonitor.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KidMonitor.Tests.Api;

public sealed class DevicePairingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DevicePairingService _service;

    public DevicePairingServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 4, 15, 30, 0, TimeSpan.Zero));
        _service = new DevicePairingService(
            _db,
            _timeProvider,
            NullLogger<DevicePairingService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GenerateAsync_CreatesPairingSession_WithSixDigitCodeAndQrPayload()
    {
        var result = await _service.GenerateAsync("pc-123", "Kid Desktop", CancellationToken.None);

        Assert.Matches("^[0-9]{6}$", result.PairingCode);
        Assert.Equal(_timeProvider.GetUtcNow().AddMinutes(10), result.ExpiresAt);
        Assert.Contains(result.PairingCode, result.QrPayload, StringComparison.Ordinal);

        var session = await _db.PairingSessions.SingleAsync();
        Assert.Equal("pc-123", session.DeviceKey);
        Assert.Equal("Kid Desktop", session.DeviceName);
        Assert.Equal(result.PairingCode, session.PairingCode);
        Assert.Equal(result.ExpiresAt.UtcDateTime, session.ExpiresAt);
    }

    [Fact]
    public async Task ClaimAsync_CreatesDeviceAndMarksPairingSessionClaimed()
    {
        var parent = await AddParentAsync("parent@example.com");
        var pairing = await _service.GenerateAsync("pc-123", "Kid Desktop", CancellationToken.None);

        var result = await _service.ClaimAsync(parent.Id, pairing.PairingCode, CancellationToken.None);

        var device = await _db.Devices.SingleAsync();
        var session = await _db.PairingSessions.SingleAsync();

        Assert.Equal(device.Id, result.DeviceId);
        Assert.Equal("Kid Desktop", result.DeviceName);
        Assert.Equal(parent.Id, device.ParentId);
        Assert.Equal("pc-123", device.DeviceKey);
        Assert.False(string.IsNullOrWhiteSpace(device.DeviceToken));
        Assert.Equal(parent.Id, session.ParentId);
        Assert.Equal(device.Id, session.DeviceId);
        Assert.NotNull(session.ClaimedAt);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsPending_WhenParentHasNotClaimedTheSessionYet()
    {
        var pairing = await _service.GenerateAsync("pc-123", "Kid Desktop", CancellationToken.None);

        var result = await _service.ConfirmAsync("pc-123", pairing.PairingCode, CancellationToken.None);

        Assert.Equal(PairingConfirmationStatus.Pending, result.Status);
        Assert.Null(result.DeviceId);
        Assert.Null(result.DeviceToken);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsCredentials_WhenSessionHasBeenClaimed()
    {
        var parent = await AddParentAsync("parent@example.com");
        var pairing = await _service.GenerateAsync("pc-123", "Kid Desktop", CancellationToken.None);
        var claim = await _service.ClaimAsync(parent.Id, pairing.PairingCode, CancellationToken.None);

        var result = await _service.ConfirmAsync("pc-123", pairing.PairingCode, CancellationToken.None);

        Assert.Equal(PairingConfirmationStatus.Confirmed, result.Status);
        Assert.Equal(claim.DeviceId, result.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(result.DeviceToken));

        var device = await _db.Devices.SingleAsync();
        Assert.Equal(device.DeviceToken, result.DeviceToken);
    }

    [Fact]
    public async Task ListDevicesAsync_ReturnsOnlyDevicesOwnedByTheRequestedParent()
    {
        var parentA = await AddParentAsync("a@example.com");
        var parentB = await AddParentAsync("b@example.com");

        _db.Devices.AddRange(
            new Device { ParentId = parentA.Id, DeviceKey = "pc-a", DeviceName = "Parent A PC", DeviceToken = "token-a" },
            new Device { ParentId = parentB.Id, DeviceKey = "pc-b", DeviceName = "Parent B PC", DeviceToken = "token-b" });
        await _db.SaveChangesAsync();

        var devices = await _service.ListDevicesAsync(parentA.Id, CancellationToken.None);

        var device = Assert.Single(devices);
        Assert.Equal("pc-a", device.DeviceKey);
    }

    [Fact]
    public async Task DeleteDeviceAsync_RemovesOnlyDevicesOwnedByTheRequestedParent()
    {
        var owner = await AddParentAsync("owner@example.com");
        var otherParent = await AddParentAsync("other@example.com");
        var ownedDevice = new Device
        {
            ParentId = owner.Id,
            DeviceKey = "pc-owner",
            DeviceName = "Owner PC",
            DeviceToken = "token-owner",
        };
        var foreignDevice = new Device
        {
            ParentId = otherParent.Id,
            DeviceKey = "pc-other",
            DeviceName = "Other PC",
            DeviceToken = "token-other",
        };

        _db.Devices.AddRange(ownedDevice, foreignDevice);
        await _db.SaveChangesAsync();

        var deletedOwned = await _service.DeleteDeviceAsync(owner.Id, ownedDevice.Id, CancellationToken.None);
        var deletedForeign = await _service.DeleteDeviceAsync(owner.Id, foreignDevice.Id, CancellationToken.None);

        Assert.True(deletedOwned);
        Assert.False(deletedForeign);
        Assert.Equal(1, await _db.Devices.CountAsync());
        Assert.Equal(foreignDevice.Id, (await _db.Devices.SingleAsync()).Id);
    }

    private async Task<Parent> AddParentAsync(string email)
    {
        var parent = new Parent
        {
            Email = email,
            PasswordHash = "hashed",
            DisplayName = email,
        };

        _db.Parents.Add(parent);
        await _db.SaveChangesAsync();
        return parent;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
