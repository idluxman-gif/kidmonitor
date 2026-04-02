using KidMonitor.Core.Models;

namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// Detects YouTube content playing in a browser window by parsing the window title.
/// Browsers format the title as "Video Title - YouTube - Browser Name" when a video is active.
/// </summary>
public class YouTubeContentAdapter : IContentCaptureAdapter
{
    // Process names for supported browsers
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera",
    };

    // Marker that indicates a YouTube tab is active in the browser
    private const string YouTubeMarker = "- YouTube";

    public bool CanCapture(ProcessWindowInfo info)
        => BrowserProcesses.Contains(info.ProcessName)
           && info.WindowTitle.Contains(YouTubeMarker, StringComparison.OrdinalIgnoreCase);

    public ContentSnapshot? TryCapture(ProcessWindowInfo info)
    {
        var (videoTitle, channel) = ParseTitle(info.WindowTitle);
        if (string.IsNullOrWhiteSpace(videoTitle))
            return null;

        return new ContentSnapshot
        {
            AppName = "YouTube",
            ContentType = ContentType.VideoTitle,
            CapturedText = videoTitle,
            Channel = channel,
            CapturedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Parses the browser window title to extract the video title.
    ///
    /// Common formats:
    ///   "Video Title - YouTube - Google Chrome"
    ///   "Video Title - YouTube"          (minimal format)
    ///   "(1) Video Title - YouTube - Google Chrome"  (with notification count)
    /// </summary>
    private static (string videoTitle, string? channel) ParseTitle(string windowTitle)
    {
        // Strip notification count prefix like "(3) "
        var title = System.Text.RegularExpressions.Regex.Replace(windowTitle, @"^\(\d+\)\s*", "");

        // Find the last occurrence of "- YouTube" and strip everything from there onward
        var ytIndex = title.LastIndexOf(YouTubeMarker, StringComparison.OrdinalIgnoreCase);
        if (ytIndex <= 0)
            return (string.Empty, null);

        var videoTitle = title[..ytIndex].Trim();

        // Some channels prepend "Channel Name: " or append " | ChannelName" — no reliable pattern
        // without DOM access, so channel stays null for title-parsed captures.
        return (videoTitle, null);
    }
}
