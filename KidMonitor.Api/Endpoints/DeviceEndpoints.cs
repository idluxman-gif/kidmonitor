using System.Security.Claims;
using System.Security.Cryptography;
using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using KidMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/devices").RequireAuthorization();

        group.MapGet("/", ListDevices);
        group.MapDelete("/{id:guid}", DeleteDevice);
        group.MapPost("/register", RegisterDevice);
    }

    private static async Task<IResult> ListDevices(
        ClaimsPrincipal user,
        [FromServices] DevicePairingService pairingService,
        CancellationToken cancellationToken)
    {
        var parentId = GetParentId(user);
        var devices = await pairingService.ListDevicesAsync(parentId, cancellationToken);
        return Results.Ok(devices.Select(device => DeviceResponse.From(device, includeToken: false)));
    }

    private static async Task<IResult> DeleteDevice(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] DevicePairingService pairingService,
        CancellationToken cancellationToken)
    {
        var parentId = GetParentId(user);
        var deleted = await pairingService.DeleteDeviceAsync(parentId, id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // POST /devices/register
    // Body: { "deviceKey": "...", "deviceName": "..." }
    private static async Task<IResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest req,
        ClaimsPrincipal user,
        [FromServices] AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceKey))
            return Results.BadRequest(new { error = "deviceKey is required." });

        var parentId = GetParentId(user);

        var existing = await db.Devices.FirstOrDefaultAsync(d => d.DeviceKey == req.DeviceKey);

        if (existing is not null)
        {
            // Device already registered — update last seen and optionally rename.
            existing.LastSeenAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(req.DeviceName))
                existing.DeviceName = req.DeviceName.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(DeviceResponse.From(existing, includeToken: false));
        }

        var device = new Device
        {
            ParentId = parentId,
            DeviceKey = req.DeviceKey.Trim(),
            DeviceName = req.DeviceName?.Trim() ?? "Unknown Device",
            DeviceToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return Results.Created($"/devices/{device.Id}", DeviceResponse.From(device, includeToken: true));
    }

    private static Guid GetParentId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
}

public record RegisterDeviceRequest(string DeviceKey, string? DeviceName);
public record DeviceResponse(Guid Id, string DeviceKey, string DeviceName, string? DeviceToken, DateTime RegisteredAt, DateTime LastSeenAt)
{
    // DeviceToken is only populated on first registration (null on re-registration since the secret is not re-exposed).
    public static DeviceResponse From(Device d, bool includeToken = false) =>
        new(d.Id, d.DeviceKey, d.DeviceName, includeToken ? d.DeviceToken : null, d.RegisteredAt, d.LastSeenAt);
}
