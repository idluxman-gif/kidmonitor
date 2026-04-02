namespace KidMonitor.Core.Configuration;

/// <summary>
/// Strongly-typed configuration options bound from appsettings.json.
/// Registered via services.Configure&lt;MonitoringOptions&gt;(config.GetSection("Monitoring")).
/// </summary>
public class MonitoringOptions
{
    public int PollIntervalSeconds { get; set; } = 10;
    public List<TrackedAppConfig> TrackedApps { get; set; } = new();
    public LanguageDetectionOptions LanguageDetection { get; set; } = new();
    public NotificationOptions Notifications { get; set; } = new();
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

    /// <summary>
    /// Minimum seconds between foul-language alert toasts per detection source
    /// (app + text/audio). Prevents notification spam on rapid detections.
    /// </summary>
    public int FoulLanguageCooldownSeconds { get; set; } = 60;
}

public class FoulLanguageOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Path to the plain-text foul word list (one word per line).</summary>
    public string WordListPath { get; set; } = @"C:\ProgramData\KidMonitor\wordlist.txt";
}

public class LanguageDetectionOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Inline word list for detection (entries are lowercased plain words).
    /// Falls back to FoulLanguage:WordListPath when this list is empty.
    /// </summary>
    public List<string> WordList { get; set; } = new();

    /// <summary>Enable audio transcription for active YouTube sessions.</summary>
    public bool AudioEnabled { get; set; } = false;

    /// <summary>Path to the GGML Whisper model file (e.g. ggml-base.bin).</summary>
    public string ModelPath { get; set; } = @"C:\ProgramData\KidMonitor\ggml-base.bin";

    /// <summary>Audio device selection: "cpu" or "cuda".</summary>
    public string GpuMode { get; set; } = "cpu";

    /// <summary>Rolling audio window length fed to Whisper, in seconds.</summary>
    public int AudioWindowSeconds { get; set; } = 8;
}

public class DatabaseOptions
{
    public string Path { get; set; } = @"C:\ProgramData\KidMonitor\kidmonitor.db";
}
