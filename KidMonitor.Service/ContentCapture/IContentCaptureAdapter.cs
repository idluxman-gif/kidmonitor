using KidMonitor.Core.Models;

namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// Adapter that detects and captures content from a specific app or class of apps.
/// Each adapter handles one category (YouTube, WhatsApp, game chat).
/// </summary>
public interface IContentCaptureAdapter
{
    /// <summary>Returns true if this adapter can process the given window.</summary>
    bool CanCapture(ProcessWindowInfo info);

    /// <summary>
    /// Attempts to capture a content snapshot from the window.
    /// Returns null if nothing meaningful could be captured.
    /// </summary>
    ContentSnapshot? TryCapture(ProcessWindowInfo info);
}
