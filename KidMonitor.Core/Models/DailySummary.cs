namespace KidMonitor.Core.Models;

/// <summary>
/// Aggregated daily usage report generated at end-of-day.
/// </summary>
public class DailySummary
{
    public int Id { get; set; }

    /// <summary>The calendar date this summary covers (local date).</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>Total screen time across all tracked apps (seconds).</summary>
    public int TotalScreenTimeSeconds { get; set; }

    /// <summary>JSON-serialised per-app breakdown: { "chrome.exe": 3600, ... }</summary>
    public string AppBreakdownJson { get; set; } = "{}";

    /// <summary>Number of foul-language events detected during the day.</summary>
    public int FoulLanguageEventCount { get; set; }

    /// <summary>Path to the generated HTML report file (may be null if not yet generated).</summary>
    public string? HtmlReportPath { get; set; }

    /// <summary>UTC timestamp when this record was created/last updated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
