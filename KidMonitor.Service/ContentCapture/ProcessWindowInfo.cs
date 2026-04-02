namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// Captures the context of a single visible top-level window used by the capture adapters.
/// </summary>
public record ProcessWindowInfo(
    string ProcessName,
    int ProcessId,
    string WindowTitle,
    nint WindowHandle
);
