using KidMonitor.Core.Models;

namespace KidMonitor.Service.ContentCapture;

/// <summary>
/// Captures game chat context from common game launchers and communication overlays.
///
/// Detected sources:
/// - Discord: standalone app; window title reflects the active server/channel.
/// - Steam: launcher and in-game overlay; title parsing gives community/friend chat context.
/// - Common games and launchers: fallback uses process name + window title.
///
/// Direct text extraction from in-game overlays is not reliably possible without
/// injecting into the rendering process, so this adapter captures context
/// (which game, which channel/server) from window titles as defined in the spec fallback.
/// </summary>
public class GameChatContentAdapter : IContentCaptureAdapter
{
    /// <summary>
    /// Map of process name (lower-case) → friendly app label.
    /// </summary>
    private static readonly Dictionary<string, string> KnownProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Communication overlays
        ["discord"]       = "Discord",
        ["discordptb"]    = "Discord PTB",
        ["discordcanary"] = "Discord Canary",

        // Game launchers with built-in chat
        ["steam"]         = "Steam",
        ["steamwebhelper"] = "Steam",

        // Common games with notable chat components
        ["leagueclient"]  = "League of Legends",
        ["riotclientservices"] = "Riot Games",
        ["epicgameslauncher"]  = "Epic Games",
        ["GenshinImpact"]      = "Genshin Impact",
        ["minecraft.windows"]  = "Minecraft",
    };

    /// <summary>Window title fragments that suggest a chat/messaging context.</summary>
    private static readonly string[] ChatSignals = ["chat", "message", "server", "channel", "lobby", "party", "voice"];

    public bool CanCapture(ProcessWindowInfo info)
        => KnownProcesses.ContainsKey(info.ProcessName);

    public ContentSnapshot? TryCapture(ProcessWindowInfo info)
    {
        var appLabel = KnownProcesses.TryGetValue(info.ProcessName, out var label)
            ? label
            : info.ProcessName;

        var title = info.WindowTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        // Build a context string: for Discord parse "ServerName - #channel - Discord"
        var (context, channel) = ParseGameChatTitle(info.ProcessName, title, appLabel);

        return new ContentSnapshot
        {
            AppName = appLabel,
            ContentType = ContentType.GameChat,
            CapturedText = context,
            Channel = channel,
            CapturedAt = DateTime.UtcNow,
        };
    }

    private static (string context, string? channel) ParseGameChatTitle(
        string processName, string windowTitle, string appLabel)
    {
        // Discord title: "Server Name - #channel-name - Discord"
        if (processName.StartsWith("discord", StringComparison.OrdinalIgnoreCase))
        {
            var parts = windowTitle.Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // Last part is "Discord"; first part is server or DM; second (if starts with #) is channel
            if (parts.Length >= 2)
            {
                var server = parts[0];
                var channelPart = parts.Length >= 3 ? parts[1] : null;
                var channel = channelPart?.StartsWith('#') == true ? channelPart : null;
                return ($"{appLabel}: {server}", channel);
            }
        }

        // Steam title: "Steam - <section>" or just "Steam"
        if (processName.Equals("steam", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase))
        {
            return ($"{appLabel}: {windowTitle}", null);
        }

        // Generic fallback
        return ($"{appLabel}: {windowTitle}", null);
    }
}
