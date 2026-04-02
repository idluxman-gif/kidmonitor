using KidMonitor.Core.Models;
using KidMonitor.Service.ContentCapture;

namespace KidMonitor.Tests.ContentCapture;

public class YouTubeContentAdapterTests
{
    private readonly YouTubeContentAdapter _sut = new();

    private static ProcessWindowInfo Win(string processName, string title)
        => new(processName, 1234, title, nint.Zero);

    // ── CanCapture ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("chrome")]
    [InlineData("msedge")]
    [InlineData("firefox")]
    [InlineData("brave")]
    [InlineData("opera")]
    public void CanCapture_ReturnsTrue_ForSupportedBrowser_WithYouTubeTitle(string browser)
    {
        Assert.True(_sut.CanCapture(Win(browser, "Cool Video - YouTube - Google Chrome")));
    }

    [Theory]
    [InlineData("CHROME")]
    [InlineData("Chrome")]
    [InlineData("MSEDGE")]
    public void CanCapture_IsCaseInsensitive_ForProcessName(string browser)
    {
        Assert.True(_sut.CanCapture(Win(browser, "Some Video - YouTube")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_ForNonBrowserProcess()
    {
        Assert.False(_sut.CanCapture(Win("vlc", "VLC Media Player")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_WhenTitleHasNoYouTubeMarker()
    {
        Assert.False(_sut.CanCapture(Win("chrome", "Google Search - Google Chrome")));
    }

    [Fact]
    public void CanCapture_ReturnsFalse_WhenTitleIsEmpty()
    {
        Assert.False(_sut.CanCapture(Win("chrome", "")));
    }

    // ── TryCapture ─────────────────────────────────────────────────────────

    [Fact]
    public void TryCapture_ExtractsVideoTitle_FromStandardFormat()
    {
        var snapshot = _sut.TryCapture(Win("chrome", "My Awesome Video - YouTube - Google Chrome"));

        Assert.NotNull(snapshot);
        Assert.Equal("My Awesome Video", snapshot!.CapturedText);
    }

    [Fact]
    public void TryCapture_ExtractsVideoTitle_FromMinimalFormat()
    {
        var snapshot = _sut.TryCapture(Win("chrome", "Short Clip - YouTube"));

        Assert.NotNull(snapshot);
        Assert.Equal("Short Clip", snapshot!.CapturedText);
    }

    [Fact]
    public void TryCapture_StripsPendingNotificationCountPrefix()
    {
        var snapshot = _sut.TryCapture(Win("chrome", "(3) Cool Video - YouTube - Google Chrome"));

        Assert.NotNull(snapshot);
        Assert.Equal("Cool Video", snapshot!.CapturedText);
    }

    [Fact]
    public void TryCapture_SetsAppNameYouTube()
    {
        var snapshot = _sut.TryCapture(Win("chrome", "Video - YouTube"))!;

        Assert.Equal("YouTube", snapshot.AppName);
    }

    [Fact]
    public void TryCapture_SetsContentTypeVideoTitle()
    {
        var snapshot = _sut.TryCapture(Win("chrome", "Video - YouTube"))!;

        Assert.Equal(ContentType.VideoTitle, snapshot.ContentType);
    }

    [Fact]
    public void TryCapture_ChannelIsNull_CannotReliablyParseFromTitleOnly()
    {
        var snapshot = _sut.TryCapture(Win("chrome", "Video Title - YouTube"))!;

        Assert.Null(snapshot.Channel);
    }

    [Fact]
    public void TryCapture_ReturnsNull_WhenYouTubeMarkerIsAtStart()
    {
        // ytIndex <= 0 means no video title before the marker
        var snapshot = _sut.TryCapture(Win("chrome", "- YouTube"));

        Assert.Null(snapshot);
    }

    [Fact]
    public void TryCapture_HandlesVideoTitleWithInternalDashes()
    {
        // Uses LastIndexOf so it finds the final "- YouTube" regardless of earlier dashes
        var snapshot = _sut.TryCapture(Win("msedge", "Best Of - Greatest Hits - YouTube - Microsoft Edge"));

        Assert.NotNull(snapshot);
        Assert.Equal("Best Of - Greatest Hits", snapshot!.CapturedText);
    }

    [Fact]
    public void TryCapture_ReturnsNull_WhenVideoTitleIsWhitespaceOnly()
    {
        // If the title before "- YouTube" is just spaces, it should return null
        var snapshot = _sut.TryCapture(Win("chrome", "  - YouTube"));

        Assert.Null(snapshot);
    }

    [Fact]
    public void TryCapture_WorksWithMsEdgeBrowser()
    {
        var snapshot = _sut.TryCapture(Win("msedge", "Tutorial - YouTube - Microsoft Edge"));

        Assert.NotNull(snapshot);
        Assert.Equal("Tutorial", snapshot!.CapturedText);
    }
}
