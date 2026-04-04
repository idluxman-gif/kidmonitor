namespace KidMonitor.Api.Models;

public class PushToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    public string Platform { get; set; } = string.Empty; // "fcm" | "apns"
    public string Token { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Parent Parent { get; set; } = null!;
}
