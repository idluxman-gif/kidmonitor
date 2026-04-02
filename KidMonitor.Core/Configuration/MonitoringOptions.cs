namespace KidMonitor.Core.Configuration;

/// <summary>
/// Strongly-typed configuration options bound from appsettings.json.
/// Registered via services.Configure&lt;MonitoringOptions&gt;(config.GetSection("Monitoring")).
/// </summary>
public class MonitoringOptions
{
    public int PollIntervalSeconds { get; set; } = 10;
    public List<TrackedAppConfig> TrackedApps { get; set; } = new();
}

public class TrackedAppConfig
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class NotificationOptions
{
    /// <summary>Minimum minutes an app must be open before triggering a start notification.</summary>
    public int AppStartThresholdMinutes { get; set; } = 5;

    /// <summary>Local time (HH:mm) when the daily summary is generated and sent.</summary>
    public string DailySummaryTimeLocal { get; set; } = "20:00";
}

public class FoulLanguageOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Path to the plain-text foul word list (one word per line).</summary>
    public string WordListPath { get; set; } = @"C:\ProgramData\KidMonitor\wordlist.txt";
}

public class DatabaseOptions
{
    public string Path { get; set; } = @"C:\ProgramData\KidMonitor\kidmonitor.db";
}
