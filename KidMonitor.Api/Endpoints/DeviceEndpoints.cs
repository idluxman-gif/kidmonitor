using System.Security.Claims;
using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/devices").RequireAuthorization();

        group.MapPost("/register", RegisterDevice);
    }

    // POST /devices/register
    // Body: { "deviceKey": "...", "deviceName": "..." }
    private static async Task<IResult> RegisterDevice(
        RegisterDeviceRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceKey))
            return Results.BadRequest(new { error = "deviceKey is required." });

        var parentId = Guid.Parse(
            user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

        var existing = await db.Devices.FirstOrDefaultAsync(d => d.DeviceKey == req.DeviceKey);

        if (existing is not null)
        {
            // Device already registered — update last seen and optionally rename.
            existing.LastSeenAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(req.DeviceName))
                existing.DeviceName = req.DeviceName.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(DeviceResponse.From(existing));
        }

        var device = new Device
        {
            ParentId = parentId,
            DeviceKey = req.DeviceKey.Trim(),
            DeviceName = req.DeviceName?.Trim() ?? "Unknown Device",
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return Results.Created($"/devices/{device.Id}", DeviceResponse.From(device));
    }
}

public record RegisterDeviceRequest(string DeviceKey, string? DeviceName);
public record DeviceResponse(Guid Id, string DeviceKey, string DeviceName, DateTime RegisteredAt, DateTime LastSeenAt)
{
    public static DeviceResponse From(Device d) =>
        new(d.Id, d.DeviceKey, d.DeviceName, d.RegisteredAt, d.LastSeenAt);
}
