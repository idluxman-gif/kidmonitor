using System.Security.Claims;
using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Endpoints;

public static class PushTokenEndpoints
{
    public static void MapPushTokenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/push-tokens").RequireAuthorization();

        group.MapPost("/", RegisterToken);
        group.MapDelete("/", RemoveToken);
    }

    // POST /push-tokens
    // Body: { "platform": "fcm|apns", "token": "..." }
    private static async Task<IResult> RegisterToken(
        RegisterPushTokenRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        if (req.Platform is not "fcm" and not "apns")
            return Results.BadRequest(new { error = "platform must be 'fcm' or 'apns'." });

        if (string.IsNullOrWhiteSpace(req.Token))
            return Results.BadRequest(new { error = "token is required." });

        var parentId = Guid.Parse(
            user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

        var existing = await db.PushTokens
            .FirstOrDefaultAsync(pt => pt.ParentId == parentId && pt.Platform == req.Platform);

        if (existing is not null)
        {
            existing.Token     = req.Token.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(PushTokenResponse.From(existing));
        }

        var pt = new PushToken
        {
            ParentId = parentId,
            Platform = req.Platform,
            Token    = req.Token.Trim(),
        };

        db.PushTokens.Add(pt);
        await db.SaveChangesAsync();

        return Results.Created($"/push-tokens/{pt.Id}", PushTokenResponse.From(pt));
    }

    // DELETE /push-tokens
    // Body: { "platform": "fcm|apns" }
    private static async Task<IResult> RemoveToken(
        RemovePushTokenRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var parentId = Guid.Parse(
            user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

        var pt = await db.PushTokens
            .FirstOrDefaultAsync(p => p.ParentId == parentId && p.Platform == req.Platform);

        if (pt is null)
            return Results.NotFound();

        db.PushTokens.Remove(pt);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}

public record RegisterPushTokenRequest(string Platform, string Token);
public record RemovePushTokenRequest(string Platform);
public record PushTokenResponse(Guid Id, string Platform, DateTime UpdatedAt)
{
    public static PushTokenResponse From(PushToken pt) =>
        new(pt.Id, pt.Platform, pt.UpdatedAt);
}
