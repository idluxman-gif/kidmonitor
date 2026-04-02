using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// Enumerates visible top-level windows via user32.dll P/Invoke and maps them
/// to ProcessWindowInfo records for use by content capture adapters.
/// </summary>
internal static class WindowEnumerator
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    private const int MaxTitleLength = 512;

    /// <summary>
    /// Returns all visible top-level windows with non-empty titles,
    /// enriched with their owning process name.
    /// </summary>
    public static IReadOnlyList<ProcessWindowInfo> GetVisibleWindows()
    {
        var results = new List<ProcessWindowInfo>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var titleLen = GetWindowTextLength(hWnd);
            if (titleLen == 0)
                return true;

            var sb = new StringBuilder(titleLen + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title))
                return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            var processName = GetProcessName((int)pid);

            results.Add(new ProcessWindowInfo(processName, (int)pid, title, hWnd));
            return true;
        }, nint.Zero);

        return results;
    }

    private static string GetProcessName(int pid)
    {
        try
        {
            return Process.GetProcessById(pid).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
