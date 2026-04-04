using dotAPNS;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Services;

public class PushNotificationService(
    AppDbContext db,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<PushNotificationService> logger) : IPushNotificationService
{
    public async Task SendPushAsync(
        Guid parentId,
        string title,
        string body,
        string notificationType,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var tokens = await db.PushTokens
            .Where(pt => pt.ParentId == parentId)
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            logger.LogDebug("No push tokens for parent {ParentId}", parentId);
            return;
        }

        foreach (var pt in tokens)
        {
            var (status, error) = pt.Platform switch
            {
                "fcm"  => await SendFcmAsync(pt, title, body, notificationType, data, ct),
                "apns" => await SendApnsAsync(pt, title, body, notificationType, data),
                _      => ("failed", $"Unknown platform '{pt.Platform}'"),
            };

            db.PushReceipts.Add(new PushReceipt
            {
                ParentId         = parentId,
                Platform         = pt.Platform,
                NotificationType = notificationType,
                Title            = title,
                Status           = status,
                ErrorMessage     = error,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ── FCM ───────────────────────────────────────────────────────────────────

    private async Task<(string status, string? error)> SendFcmAsync(
        PushToken pt,
        string title,
        string body,
        string notificationType,
        Dictionary<string, string>? extraData,
        CancellationToken ct)
    {
        var messaging = GetFirebaseMessaging();
        if (messaging is null)
        {
            logger.LogWarning("FCM not configured; skipping token {TokenId}", pt.Id);
            return ("failed", "FCM not configured");
        }

        var payload = new Dictionary<string, string>(extraData ?? [])
        {
            ["notificationType"] = notificationType,
        };

        var message = new Message
        {
            Token        = pt.Token,
            Notification = new Notification { Title = title, Body = body },
            Data         = payload,
            Android      = new AndroidConfig { Priority = Priority.High },
        };

        try
        {
            await messaging.SendAsync(message, ct);
            logger.LogInformation("FCM sent for parent {ParentId}", pt.ParentId);
            return ("sent", null);
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(ex, "FCM send failed for token {TokenId}: {Code}", pt.Id, ex.MessagingErrorCode);

            if (ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                RemoveToken(pt);

            return ("failed", ex.MessagingErrorCode.ToString());
        }
    }

    private static FirebaseMessaging? GetFirebaseMessaging()
    {
        // FirebaseApp is a static singleton initialised during startup.
        // Return null when not configured so callers can skip gracefully.
        try
        {
            return FirebaseMessaging.DefaultInstance;
        }
        catch
        {
            return null;
        }
    }

    // ── APNs ─────────────────────────────────────────────────────────────────

    private async Task<(string status, string? error)> SendApnsAsync(
        PushToken pt,
        string title,
        string body,
        string notificationType,
        Dictionary<string, string>? extraData)
    {
        var apnsClient = BuildApnsClient();
        if (apnsClient is null)
        {
            logger.LogWarning("APNs not configured; skipping token {TokenId}", pt.Id);
            return ("failed", "APNs not configured");
        }

        var push = new ApplePush(ApplePushType.Alert)
            .AddToken(pt.Token)
            .AddAlert(title, body)
            .AddCustomProperty("notificationType", notificationType);

        if (extraData is not null)
        {
            foreach (var (k, v) in extraData)
                push.AddCustomProperty(k, v);
        }

        try
        {
            var response = await apnsClient.SendAsync(push);

            if (response.IsSuccessful)
            {
                logger.LogInformation("APNs sent for parent {ParentId}", pt.ParentId);
                return ("sent", null);
            }

            logger.LogWarning("APNs rejected token {TokenId}: {Reason}", pt.Id, response.ReasonString);

            if (response.Reason is ApnsResponseReason.BadDeviceToken or ApnsResponseReason.Unregistered)
                RemoveToken(pt);

            return ("failed", response.ReasonString);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "APNs send threw for token {TokenId}", pt.Id);
            return ("failed", ex.Message);
        }
    }

    private IApnsClient? BuildApnsClient()
    {
        var keyId      = config["Apns:KeyId"];
        var teamId     = config["Apns:TeamId"];
        var bundleId   = config["Apns:BundleId"];
        var keyPath    = config["Apns:P8KeyPath"];
        var keyContent = config["Apns:P8KeyContent"];

        if (string.IsNullOrWhiteSpace(keyId)    ||
            string.IsNullOrWhiteSpace(teamId)   ||
            string.IsNullOrWhiteSpace(bundleId) ||
            (string.IsNullOrWhiteSpace(keyPath) && string.IsNullOrWhiteSpace(keyContent)))
            return null;

        var options = new ApnsJwtOptions
        {
            BundleId = bundleId,
            TeamId   = teamId,
            KeyId    = keyId,
        };

        if (!string.IsNullOrWhiteSpace(keyContent))
            options.CertContent = keyContent;
        else
            options.CertFilePath = keyPath;

        // The named HttpClient sets the base address for sandbox vs production.
        var useSandbox = config.GetValue<bool>("Apns:UseSandbox");
        var httpClient = httpClientFactory.CreateClient(useSandbox ? "apns-sandbox" : "apns-production");

        return ApnsClient.CreateUsingJwt(httpClient, options);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RemoveToken(PushToken pt)
    {
        logger.LogInformation("Removing stale push token {TokenId} (platform={Platform})", pt.Id, pt.Platform);
        db.PushTokens.Remove(pt);
        // SaveChangesAsync is called after all tokens are processed in SendPushAsync.
    }
}
