using System.Security.Cryptography;
using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Services;

public sealed class DevicePairingService(
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<DevicePairingService> logger)
{
    private static readonly TimeSpan PairingTtl = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db = db;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<DevicePairingService> _logger = logger;

    public async Task<PairingGenerationResult> GenerateAsync(
        string deviceKey,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        var normalizedDeviceKey = NormalizeRequired(deviceKey, nameof(deviceKey));
        var normalizedDeviceName = NormalizeOptional(deviceName, "Unknown Device");

        await RemoveExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);

        var existingSessions = await _db.PairingSessions
            .Where(session => session.DeviceKey == normalizedDeviceKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existingSessions.Count > 0)
        {
            _db.PairingSessions.RemoveRange(existingSessions);
        }

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(PairingTtl);
        var pairingCode = await GenerateUniquePairingCodeAsync(cancellationToken).ConfigureAwait(false);
        var session = new PairingSession
        {
            PairingCode = pairingCode,
            DeviceKey = normalizedDeviceKey,
            DeviceName = normalizedDeviceName,
            CreatedAt = now.UtcDateTime,
            ExpiresAt = expiresAt.UtcDateTime,
        };

        _db.PairingSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PairingGenerationResult(
            pairingCode,
            BuildQrPayload(pairingCode, normalizedDeviceName),
            expiresAt);
    }

    public async Task<PairingClaimResult> ClaimAsync(
        Guid parentId,
        string pairingCode,
        CancellationToken cancellationToken)
    {
        var session = await GetActiveSessionAsync(pairingCode, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Pairing code is invalid or expired.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var device = await _db.Devices
            .FirstOrDefaultAsync(existing => existing.DeviceKey == session.DeviceKey, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
        {
            device = new Device
            {
                ParentId = parentId,
                DeviceKey = session.DeviceKey,
                DeviceName = session.DeviceName,
                DeviceToken = CreateDeviceToken(),
                RegisteredAt = now,
                LastSeenAt = now,
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.ParentId = parentId;
            device.DeviceName = session.DeviceName;
            device.DeviceToken = CreateDeviceToken();
            device.LastSeenAt = now;
        }

        session.ParentId = parentId;
        session.Device = device;
        session.DeviceId = device.Id;
        session.ClaimedAt ??= now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PairingClaimResult(
            device.Id,
            device.DeviceName,
            new DateTimeOffset(session.ExpiresAt, TimeSpan.Zero));
    }

    public async Task<PairingConfirmationResult> ConfirmAsync(
        string deviceKey,
        string pairingCode,
        CancellationToken cancellationToken)
    {
        var normalizedDeviceKey = NormalizeRequired(deviceKey, nameof(deviceKey));
        var session = await GetActiveSessionAsync(pairingCode, cancellationToken).ConfigureAwait(false);
        if (session is null || session.DeviceKey != normalizedDeviceKey)
        {
            return new PairingConfirmationResult(PairingConfirmationStatus.Expired, null, null, null);
        }

        if (session.DeviceId is null)
        {
            return new PairingConfirmationResult(PairingConfirmationStatus.Pending, null, null, null);
        }

        var device = await _db.Devices
            .FirstOrDefaultAsync(existing => existing.Id == session.DeviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return new PairingConfirmationResult(PairingConfirmationStatus.Expired, null, null, null);
        }

        session.ConfirmedAt ??= _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PairingConfirmationResult(
            PairingConfirmationStatus.Confirmed,
            device.Id,
            device.DeviceToken,
            device.DeviceName);
    }

    public async Task<IReadOnlyList<Device>> ListDevicesAsync(Guid parentId, CancellationToken cancellationToken)
    {
        return await _db.Devices
            .Where(device => device.ParentId == parentId)
            .OrderBy(device => device.DeviceName)
            .ThenBy(device => device.RegisteredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteDeviceAsync(Guid parentId, Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await _db.Devices
            .FirstOrDefaultAsync(existing => existing.Id == deviceId && existing.ParentId == parentId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return false;
        }

        _db.Devices.Remove(device);

        var linkedSessions = await _db.PairingSessions
            .Where(session => session.DeviceId == deviceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (linkedSessions.Count > 0)
        {
            _db.PairingSessions.RemoveRange(linkedSessions);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<PairingSession?> GetActiveSessionAsync(
        string pairingCode,
        CancellationToken cancellationToken)
    {
        var normalizedPairingCode = NormalizeRequired(pairingCode, nameof(pairingCode));
        await RemoveExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);

        return await _db.PairingSessions
            .Include(session => session.Device)
            .FirstOrDefaultAsync(session => session.PairingCode == normalizedPairingCode, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RemoveExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiredSessions = await _db.PairingSessions
            .Where(session => session.ExpiresAt <= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (expiredSessions.Count == 0)
        {
            return;
        }

        _db.PairingSessions.RemoveRange(expiredSessions);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Removed {Count} expired pairing session(s).", expiredSessions.Count);
    }

    private async Task<string> GenerateUniquePairingCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var exists = await _db.PairingSessions
                .AnyAsync(session => session.PairingCode == candidate, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique pairing code.");
    }

    private static string BuildQrPayload(string pairingCode, string deviceName) =>
        $"kidmonitor://pair?code={Uri.EscapeDataString(pairingCode)}&deviceName={Uri.EscapeDataString(deviceName)}";

    private static string CreateDeviceToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static string NormalizeRequired(string value, string paramName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return normalized;
    }

    private static string NormalizeOptional(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed record PairingGenerationResult(string PairingCode, string QrPayload, DateTimeOffset ExpiresAt);

public sealed record PairingClaimResult(Guid DeviceId, string DeviceName, DateTimeOffset ExpiresAt);

public sealed record PairingConfirmationResult(
    PairingConfirmationStatus Status,
    Guid? DeviceId,
    string? DeviceToken,
    string? DeviceName);

public enum PairingConfirmationStatus
{
    Pending,
    Confirmed,
    Expired,
}
