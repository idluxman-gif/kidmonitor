using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using KidMonitor.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KidMonitor.Api.Endpoints;

public static class EventEndpoints
{
    private const int RateLimitPerMinute = 60;

    public static void MapEventEndpoints(this WebApplication app)
    {
        app.MapPost("/events", IngestEvent);
    }

    // POST /events
    // Header: Authorization: Bearer <device-token>
    // Body: { "eventType": "...", "timestamp": "...", "metadata": { ... } }
    private static async Task<IResult> IngestEvent(
        IngestEventRequest req,
        HttpContext http,
        AppDbContext db,
        IPushNotificationService push,
        IMemoryCache cache,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("KidMonitor.Api.Events");

        // ── Device authentication ────────────────────────────────────────────
        var rawAuth = http.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawAuth) || !rawAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();

        var deviceToken = rawAuth["Bearer ".Length..].Trim();

        var device = await db.Devices
            .Include(d => d.Parent)
            .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken);

        if (device is null)
            return Results.Unauthorized();

        // ── Rate limiting (60 events/min per device) ─────────────────────────
        // Use a per-minute bucket key so the counter resets automatically each minute.
        var minuteBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var rateKey = $"evt_rate:{device.Id}:{minuteBucket}";
        if (!cache.TryGetValue(rateKey, out int count))
            count = 0;

        if (count >= RateLimitPerMinute)
        {
            logger.LogWarning("Rate limit exceeded for device {DeviceId}", device.Id);
            return Results.StatusCode(429);
        }

        cache.Set(rateKey, count + 1, TimeSpan.FromMinutes(2));

        // ── Validate event type ───────────────────────────────────────────────
        var validTypes = new HashSet<string> { "app_opened", "foul_language_detected", "url_visited", "session_summary" };
        if (string.IsNullOrWhiteSpace(req.EventType) || !validTypes.Contains(req.EventType))
            return Results.BadRequest(new { error = $"eventType must be one of: {string.Join(", ", validTypes)}" });

        // ── Persist event ─────────────────────────────────────────────────────
        var occurredAt = req.Timestamp.HasValue
            ? req.Timestamp.Value.UtcDateTime
            : DateTime.UtcNow;

        var metadataJson = req.Metadata is not null
            ? System.Text.Json.JsonSerializer.Serialize(req.Metadata)
            : "{}";

        var ev = new Event
        {
            DeviceId   = device.Id,
            Kind       = req.EventType,
            Payload    = metadataJson,
            OccurredAt = occurredAt,
            ReceivedAt = DateTime.UtcNow,
        };

        db.Events.Add(ev);

        // Update device last-seen
        device.LastSeenAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── Dispatch push notification ────────────────────────────────────────
        _ = Task.Run(async () =>
        {
            try
            {
                var (title, body) = BuildNotification(req.EventType, req.Metadata, device.DeviceName);
                await push.SendPushAsync(
                    device.ParentId,
                    title,
                    body,
                    req.EventType,
                    data: new Dictionary<string, string>
                    {
                        ["eventId"]    = ev.Id.ToString(),
                        ["deviceId"]   = device.Id.ToString(),
                        ["deviceName"] = device.DeviceName,
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push dispatch failed for event {EventId}", ev.Id);
            }
        });

        return Results.Accepted($"/events/{ev.Id}", new { eventId = ev.Id });
    }

    private static (string title, string body) BuildNotification(
        string eventType,
        Dictionary<string, object?>? metadata,
        string deviceName)
    {
        return eventType switch
        {
            "app_opened" => (
                $"App opened on {deviceName}",
                metadata?.TryGetValue("appName", out var app) == true && app is not null
                    ? $"{app} was opened."
                    : "An app was opened."),

            "foul_language_detected" => (
                $"Alert on {deviceName}",
                "Foul language was detected."),

            "url_visited" => (
                $"Website visited on {deviceName}",
                metadata?.TryGetValue("url", out var url) == true && url is not null
                    ? $"Visited: {url}"
                    : "A website was visited."),

            "session_summary" => (
                $"Daily summary for {deviceName}",
                "Your child's daily activity summary is ready."),

            _ => ("KidMonitor Alert", "A monitoring event was recorded."),
        };
    }
}

public record IngestEventRequest(
    string EventType,
    DateTimeOffset? Timestamp,
    Dictionary<string, object?>? Metadata);
