namespace KidMonitor.Api.Models;

public class PairingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PairingCode { get; set; } = string.Empty;
    public string DeviceKey { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public Parent? Parent { get; set; }
    public Device? Device { get; set; }
}
