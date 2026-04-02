using KidMonitor.Core.Models;
using KidMonitor.Service.ContentCapture;

namespace KidMonitor.Tests.ContentCapture;

public class GameChatContentAdapterTests
{
    private readonly GameChatContentAdapter _sut = new();

    private static ProcessWindowInfo Win(string processName, string title)
        => new(processName, 1234, title, nint.Zero);

    // ── CanCapture ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("discord")]
    [InlineData("discordptb")]
    [InlineData("discordcanary")]
    [InlineData("steam")]
    [InlineData("steamwebhelper")]
    [InlineData("leagueclient")]
    [InlineData("riotclientservices")]
    [InlineData("epicgameslauncher")]
    public void CanCapture_ReturnsTrue_ForKnownProcesses(string process)
    {
        Assert.True(_sut.CanCapture(Win(process, "Some Title")));
    }

    [Fact]
    public void CanCapture_IsCaseInsensitive()
    {
        Assert.True(_sut.CanCapture(Win("DISCORD", "Discord")));
        Assert.True(_sut.CanCapture(Win("Steam", "Steam")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForNotepad()
    {
        Assert.False(_sut.CanCapture(Win("notepad", "Untitled")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForChrome()
    {
        Assert.False(_sut.CanCapture(Win("chrome", "Google Chrome")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForUnknownGame()
    {
        Assert.False(_sut.CanCapture(Win("mygame", "My Custom Game")));
    }

    // ── TryCapture — Discord parsing ───────────────────────────────────────

    [Fact]
    public void TryCapture_Discord_ParsesServerAndChannel()
    {
        var snapshot = _sut.TryCapture(Win("discord", "MyServer - #general - Discord"));

        Assert.NotNull(snapshot);
        Assert.Equal("Discord", snapshot!.AppName);
        Assert.Contains("MyServer", snapshot.CapturedText);
        Assert.Equal("#general", snapshot.Channel);
    }

    [Fact]
    public void TryCapture_Discord_SetsContentTypeGameChat()
    {
        var snapshot = _sut.TryCapture(Win("discord", "Server - #channel - Discord"))!;

        Assert.Equal(ContentType.GameChat, snapshot.ContentType);
    }

    [Fact]
    public void TryCapture_Discord_NullChannel_WhenNoHashPrefix()
    {
        // Second part doesn't start with '#' → no channel extracted
        var snapshot = _sut.TryCapture(Win("discord", "Some Server - Discord"));

        Assert.NotNull(snapshot);
        Assert.Null(snapshot!.Channel);
    }

    [Fact]
    public void TryCapture_Discord_NullChannel_WhenOnlyTwoParts()
    {
        // Only "ServerName - Discord" → parts.Length < 3 means no channel part
        var snapshot = _sut.TryCapture(Win("discord", "ServerName - Discord"));

        Assert.NotNull(snapshot);
        Assert.Null(snapshot!.Channel);
    }

    [Fact]
    public void TryCapture_DiscordPTB_IsRecognizedWithCorrectAppName()
    {
        var snapshot = _sut.TryCapture(Win("discordptb", "MyServer - #lobby - Discord PTB"));

        Assert.NotNull(snapshot);
        Assert.Equal("Discord PTB", snapshot!.AppName);
    }

    [Fact]
    public void TryCapture_DiscordCanary_IsRecognizedWithCorrectAppName()
    {
        var snapshot = _sut.TryCapture(Win("discordcanary", "MyServer - #test - Discord Canary"));

        Assert.NotNull(snapshot);
        Assert.Equal("Discord Canary", snapshot!.AppName);
    }

    // ── TryCapture — Steam ────────────────────────────────────────────────

    [Fact]
    public void TryCapture_Steam_ReturnsSteamContext_NullChannel()
    {
        var snapshot = _sut.TryCapture(Win("steam", "Steam - Friends & Chat"));

        Assert.NotNull(snapshot);
        Assert.Equal("Steam", snapshot!.AppName);
        Assert.Contains("Steam", snapshot.CapturedText);
        Assert.Null(snapshot.Channel);
    }

    [Fact]
    public void TryCapture_SteamWebHelper_MapsToSteamAppName()
    {
        var snapshot = _sut.TryCapture(Win("steamwebhelper", "Steam Store"));

        Assert.NotNull(snapshot);
        Assert.Equal("Steam", snapshot!.AppName);
    }

    // ── TryCapture — Generic fallback ─────────────────────────────────────

    [Fact]
    public void TryCapture_LeagueClient_UsesGenericFallback_WithFriendlyName()
    {
        var snapshot = _sut.TryCapture(Win("leagueclient", "League of Legends - Lobby"));

        Assert.NotNull(snapshot);
        Assert.Equal("League of Legends", snapshot!.AppName);
        Assert.Contains("League of Legends", snapshot.CapturedText);
    }

    [Fact]
    public void TryCapture_EpicGames_UsesGenericFallback()
    {
        var snapshot = _sut.TryCapture(Win("epicgameslauncher", "Epic Games Launcher"));

        Assert.NotNull(snapshot);
        Assert.Equal("Epic Games", snapshot!.AppName);
    }

    // ── TryCapture — null/empty title guard ───────────────────────────────

    [Fact]
    public void TryCapture_ReturnsNull_WhenWindowTitleIsEmpty()
    {
        Assert.Null(_sut.TryCapture(Win("discord", "")));
    }

    [Fact]
    public void TryCapture_ReturnsNull_WhenWindowTitleIsWhitespace()
    {
        Assert.Null(_sut.TryCapture(Win("discord", "   ")));
    }
}
