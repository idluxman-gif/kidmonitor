namespace KidMonitor.Api.Models;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string Kind { get; set; } = string.Empty;   // e.g. "app_usage", "content_alert", "daily_summary"
    public string Payload { get; set; } = string.Empty; // JSON blob
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public Device Device { get; set; } = null!;
}
