using System.Security.Claims;
using KidMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KidMonitor.Api.Endpoints;

public static class PairingEndpoints
{
    public static void MapPairingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/pairing");

        group.MapPost("/generate", GeneratePairing);
        group.MapPost("/claim", ClaimPairing).RequireAuthorization();
        group.MapPost("/confirm", ConfirmPairing);
    }

    private static async Task<IResult> GeneratePairing(
        [FromBody] GeneratePairingRequest request,
        DevicePairingService pairingService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await pairingService.GenerateAsync(
                request.DeviceKey,
                request.DeviceName,
                cancellationToken);

            return Results.Created(
                $"/pairing/{result.PairingCode}",
                new GeneratePairingResponse(result.PairingCode, result.QrPayload, result.ExpiresAt));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ClaimPairing(
        [FromBody] ClaimPairingRequest request,
        ClaimsPrincipal user,
        DevicePairingService pairingService,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = Guid.Parse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
            var result = await pairingService.ClaimAsync(parentId, request.PairingCode, cancellationToken);
            return Results.Ok(new ClaimPairingResponse(result.DeviceId, result.DeviceName, result.ExpiresAt));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ConfirmPairing(
        [FromBody] ConfirmPairingRequest request,
        DevicePairingService pairingService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await pairingService.ConfirmAsync(
                request.DeviceKey,
                request.PairingCode,
                cancellationToken);

            return result.Status switch
            {
                PairingConfirmationStatus.Pending => Results.Accepted(
                    $"/pairing/{request.PairingCode}",
                    ConfirmPairingResponse.From(result)),
                PairingConfirmationStatus.Confirmed => Results.Ok(ConfirmPairingResponse.From(result)),
                PairingConfirmationStatus.Expired => Results.NotFound(new { error = "Pairing code is invalid or expired." }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record GeneratePairingRequest(string DeviceKey, string? DeviceName);

public sealed record ClaimPairingRequest(string PairingCode);

public sealed record ConfirmPairingRequest(string DeviceKey, string PairingCode);

public sealed record GeneratePairingResponse(string PairingCode, string QrPayload, DateTimeOffset ExpiresAt);

public sealed record ClaimPairingResponse(Guid DeviceId, string DeviceName, DateTimeOffset ExpiresAt);

public sealed record ConfirmPairingResponse(
    string Status,
    Guid? DeviceId,
    string? DeviceToken,
    string? DeviceName)
{
    public static ConfirmPairingResponse From(PairingConfirmationResult result) =>
        new(result.Status.ToString().ToLowerInvariant(), result.DeviceId, result.DeviceToken, result.DeviceName);
}
