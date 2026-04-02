using System.Runtime.InteropServices;
using System.Text;
using KidMonitor.Core.Models;

namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// Captures content from WhatsApp Desktop.
///
/// Two-level approach:
/// 1. Window title — always available; gives the active contact/group name
///    (WhatsApp Desktop formats the title as "Contact Name | WhatsApp" or just "WhatsApp").
/// 2. UI Automation child-window text — attempts to read visible message text
///    from WhatsApp's Chromium Embedded Framework (CEF) renderer child window
///    via WM_GETTEXT on focusable child controls. This is a best-effort heuristic;
///    most message content in the Electron shell is rendered in a web layer that
///    is not directly accessible without injecting into the renderer process.
///    Full accessibility-tree walking via IUIAutomation can be added if needed.
/// </summary>
public class WhatsAppContentAdapter : IContentCaptureAdapter
{
    private const string WhatsAppProcessName = "WhatsApp";
    private const string WhatsAppTitleSuffix = "| WhatsApp";
    private const string WhatsAppWindowClass = "WhatsApp";

    [DllImport("user32.dll")]
    private static extern nint FindWindowEx(nint hWndParent, nint hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int SendMessage(nint hWnd, uint Msg, nint wParam, StringBuilder lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(nint hWndParent, EnumChildProc lpEnumFunc, nint lParam);

    private delegate bool EnumChildProc(nint hWnd, nint lParam);

    private const uint WM_GETTEXT = 0x000D;
    private const uint WM_GETTEXTLENGTH = 0x000E;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    public bool CanCapture(ProcessWindowInfo info)
        => info.ProcessName.Equals(WhatsAppProcessName, StringComparison.OrdinalIgnoreCase);

    public ContentSnapshot? TryCapture(ProcessWindowInfo info)
    {
        var contactName = ExtractContactName(info.WindowTitle);

        // Attempt to read visible message text from child windows
        var messageText = TryReadChildText(info.WindowHandle);

        var capturedText = messageText ?? contactName;
        if (string.IsNullOrWhiteSpace(capturedText))
            return null;

        return new ContentSnapshot
        {
            AppName = "WhatsApp Desktop",
            ContentType = ContentType.MessageText,
            CapturedText = capturedText,
            Channel = contactName,
            CapturedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Extracts the contact/group name from the WhatsApp window title.
    /// Title format: "Contact Name | WhatsApp" or just "WhatsApp".
    /// </summary>
    private static string ExtractContactName(string windowTitle)
    {
        var idx = windowTitle.IndexOf(WhatsAppTitleSuffix, StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
            return windowTitle[..idx].Trim();

        // Window just shows "WhatsApp" — no specific conversation is focused
        return string.Empty;
    }

    /// <summary>
    /// Walks immediate child windows looking for text-bearing edit or static controls.
    /// WhatsApp Desktop (Electron/CEF) may expose a Chrome_WidgetWin_1 host with
    /// child RenderWidgetHostHWND controls; direct WM_GETTEXT rarely yields message
    /// content from those, but we attempt it for any visible child with text.
    /// </summary>
    private static string? TryReadChildText(nint parentHwnd)
    {
        var texts = new List<string>();

        EnumChildWindows(parentHwnd, (hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var sb = new StringBuilder(1024);
            int len = GetWindowText(hWnd, sb, sb.Capacity);
            if (len > 0)
            {
                var text = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 2)
                    texts.Add(text);
            }
            return true;
        }, nint.Zero);

        return texts.Count > 0 ? string.Join(" | ", texts.Take(3)) : null;
    }
}
