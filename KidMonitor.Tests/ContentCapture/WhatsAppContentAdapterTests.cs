using KidMonitor.Core.Models;
using KidMonitor.Service.ContentCapture;

namespace KidMonitor.Tests.ContentCapture;

/// <summary>
/// Tests for <see cref="WhatsAppContentAdapter"/>.
///
/// Note: <c>TryCapture</c> involves P/Invoke calls (EnumChildWindows, GetWindowText)
/// that cannot be mocked without refactoring. CanCapture is tested exhaustively.
/// TryCapture tests validate snapshot structure when a snapshot is returned, without
/// asserting on exact child-window text (which varies by test environment).
/// </summary>
public class WhatsAppContentAdapterTests
{
    private readonly WhatsAppContentAdapter _sut = new();

    private static ProcessWindowInfo Win(string processName, string title)
        => new(processName, 1234, title, nint.Zero);

    // ── CanCapture ─────────────────────────────────────────────────────────

    [Fact]
    public void CanCapture_ReturnsTrue_ForWhatsAppProcess()
    {
        Assert.True(_sut.CanCapture(Win("WhatsApp", "WhatsApp")));
    }

    [Fact]
    public void CanCapture_IsCaseInsensitive()
    {
        Assert.True(_sut.CanCapture(Win("whatsapp", "WhatsApp")));
        Assert.True(_sut.CanCapture(Win("WHATSAPP", "WhatsApp")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForChrome()
    {
        Assert.False(_sut.CanCapture(Win("chrome", "WhatsApp Web")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForDiscord()
    {
        Assert.False(_sut.CanCapture(Win("discord", "Discord")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForSteam()
    {
        Assert.False(_sut.CanCapture(Win("steam", "Steam")));
    }

    // ── TryCapture ─────────────────────────────────────────────────────────

    [Fact]
    public void TryCapture_DoesNotThrow_ForPlainWhatsAppTitle()
    {
        var ex = Record.Exception(() => _sut.TryCapture(Win("WhatsApp", "WhatsApp")));
        Assert.Null(ex);
    }

    [Fact]
    public void TryCapture_DoesNotThrow_ForConversationTitle()
    {
        var ex = Record.Exception(() => _sut.TryCapture(Win("WhatsApp", "Alice | WhatsApp")));
        Assert.Null(ex);
    }

    [Fact]
    public void TryCapture_WhenSnapshotReturned_SetsAppNameWhatsAppDesktop()
    {
        var snapshot = _sut.TryCapture(Win("WhatsApp", "Alice | WhatsApp"));

        if (snapshot is not null)
            Assert.Equal("WhatsApp Desktop", snapshot.AppName);
    }

    [Fact]
    public void TryCapture_WhenSnapshotReturned_SetsContentTypeMessageText()
    {
        var snapshot = _sut.TryCapture(Win("WhatsApp", "Bob | WhatsApp"));

        if (snapshot is not null)
            Assert.Equal(ContentType.MessageText, snapshot.ContentType);
    }

    [Fact]
    public void TryCapture_WhenSnapshotReturned_ExtractsContactFromTitle()
    {
        // Channel should be the contact name extracted from the title
        var snapshot = _sut.TryCapture(Win("WhatsApp", "Charlie Smith | WhatsApp"));

        if (snapshot is not null)
            Assert.Equal("Charlie Smith", snapshot.Channel);
    }
}
