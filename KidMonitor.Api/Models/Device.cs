namespace KidMonitor.Api.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    /// <summary>Unique opaque key generated on the PC side (e.g. machine fingerprint hash).</summary>
    public string DeviceKey { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public Parent Parent { get; set; } = null!;
    public List<Event> Events { get; set; } = [];
}
