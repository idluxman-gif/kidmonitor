namespace KidMonitor.Core.Models;

/// <summary>
/// Records a single app usage session for a monitored process.
/// </summary>
public class AppSession
{
    public int Id { get; set; }

    /// <summary>Executable name (e.g. "chrome.exe").</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Friendly display name (e.g. "Google Chrome").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>UTC time when the process was first observed running.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>UTC time when the process stopped (null = still running).</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Total duration in seconds (computed on close).</summary>
    public int DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
