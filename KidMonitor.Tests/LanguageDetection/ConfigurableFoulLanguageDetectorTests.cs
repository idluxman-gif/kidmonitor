using KidMonitor.Core.Configuration;
using KidMonitor.Service.LanguageDetection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace KidMonitor.Tests.LanguageDetection;

public class ConfigurableFoulLanguageDetectorTests
{
    /// <summary>
    /// Builds a detector with an inline word list sourced from MonitoringOptions.
    /// No file-system dependency — tests are hermetic.
    /// </summary>
    private static ConfigurableFoulLanguageDetector Build(
        List<string>? wordList = null,
        bool enabled = true)
    {
        var monOptions = new MonitoringOptions
        {
            LanguageDetection = new LanguageDetectionOptions
            {
                Enabled = enabled,
                WordList = wordList ?? new List<string>(),
            }
        };

        var optionsMon = new Mock<IOptionsMonitor<MonitoringOptions>>();
        optionsMon.Setup(m => m.CurrentValue).Returns(monOptions);
        optionsMon
            .Setup(m => m.OnChange(It.IsAny<Action<MonitoringOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var legacyOptions = new FoulLanguageOptions { WordListPath = string.Empty };
        var legacyMon = new Mock<IOptionsMonitor<FoulLanguageOptions>>();
        legacyMon.Setup(m => m.CurrentValue).Returns(legacyOptions);
        legacyMon
            .Setup(m => m.OnChange(It.IsAny<Action<FoulLanguageOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        return new ConfigurableFoulLanguageDetector(
            optionsMon.Object,
            legacyMon.Object,
            NullLogger<ConfigurableFoulLanguageDetector>.Instance);
    }

    // ── Empty / disabled edge cases ────────────────────────────────────────

    [Fact]
    public void Scan_ReturnsEmpty_WhenTextIsEmpty()
    {
        var sut = Build(new List<string> { "bad" });
        Assert.Empty(sut.Scan("", "YouTube"));
    }

    [Fact]
    public void Scan_ReturnsEmpty_WhenTextIsWhitespace()
    {
        var sut = Build(new List<string> { "bad" });
        Assert.Empty(sut.Scan("   ", "YouTube"));
    }

    [Fact]
    public void Scan_ReturnsEmpty_WhenDetectionDisabled()
    {
        var sut = Build(new List<string> { "bad" }, enabled: false);
        Assert.Empty(sut.Scan("this is bad", "YouTube"));
    }

    [Fact]
    public void Scan_ReturnsEmpty_WhenWordListIsEmpty()
    {
        var sut = Build(new List<string>());
        Assert.Empty(sut.Scan("this is bad", "YouTube"));
    }

    // ── Exact matching ─────────────────────────────────────────────────────

    [Fact]
    public void Scan_DetectsExactMatch()
    {
        var sut = Build(new List<string> { "badword" });

        var results = sut.Scan("this is a badword here", "YouTube");

        Assert.Single(results);
        Assert.Equal("badword", results[0].MatchedTerm);
    }

    [Fact]
    public void Scan_IsCaseInsensitive()
    {
        var sut = Build(new List<string> { "badword" });

        var results = sut.Scan("I said BADWORD today", "YouTube");

        Assert.Single(results);
        Assert.Equal("badword", results[0].MatchedTerm);
    }

    [Fact]
    public void Scan_DetectsMultipleDistinctWords()
    {
        var sut = Build(new List<string> { "foo", "bar" });

        var results = sut.Scan("I say foo and bar here", "App");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Scan_DetectsWordAtStartOfText()
    {
        var sut = Build(new List<string> { "bad" });

        var results = sut.Scan("bad start of message", "App");

        Assert.Single(results);
    }

    [Fact]
    public void Scan_DetectsWordAtEndOfText()
    {
        var sut = Build(new List<string> { "bad" });

        var results = sut.Scan("this ends with bad", "App");

        Assert.Single(results);
    }

    // ── Word-boundary guard ────────────────────────────────────────────────

    [Fact]
    public void Scan_DoesNotFlag_TargetWordInsideLongerWord()
    {
        // "ass" should not match inside "assassin" or "classic"
        var sut = Build(new List<string> { "ass" });

        var results = sut.Scan("The assassin crept through the passage", "App");

        Assert.Empty(results);
    }

    [Fact]
    public void Scan_Flags_WordAdjacentToPunctuation()
    {
        var sut = Build(new List<string> { "bad" });

        var results = sut.Scan("that's bad! really bad.", "App");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Scan_Flags_WordSurroundedBySpaces()
    {
        var sut = Build(new List<string> { "jerk" });

        var results = sut.Scan("you are a jerk ok", "App");

        Assert.Single(results);
    }

    // ── L33tspeak normalisation ────────────────────────────────────────────

    [Fact]
    public void Scan_Detects_L33tFourForA()
    {
        // "b4d" → normalised to "bad"
        var sut = Build(new List<string> { "bad" });

        var results = sut.Scan("this is b4d", "App");

        Assert.Single(results);
        Assert.Equal("bad", results[0].MatchedTerm);
    }

    [Fact]
    public void Scan_Detects_L33tAtSignForA()
    {
        // "@ss" → 'a' + "ss" → "ass"
        var sut = Build(new List<string> { "ass" });

        var results = sut.Scan("what @ss said that", "App");

        Assert.Single(results);
    }

    [Fact]
    public void Scan_Detects_L33tOneForI()
    {
        // "1d10t" → 'i','d','i','o','t' → "idiot"
        var sut = Build(new List<string> { "idiot" });

        var results = sut.Scan("you are 1d10t", "App");

        Assert.Single(results);
    }

    [Fact]
    public void Scan_Detects_L33tZeroForO()
    {
        // "f00l" → "fool"
        var sut = Build(new List<string> { "fool" });

        var results = sut.Scan("you f00l!", "App");

        Assert.Single(results);
    }

    [Fact]
    public void Scan_Detects_L33tThreeForE()
    {
        // "3" → 'e'
        var sut = Build(new List<string> { "leet" });

        var results = sut.Scan("this is l33t speak", "App");

        Assert.Single(results);
    }

    [Fact]
    public void Scan_Detects_L33tDollarSignForS()
    {
        // "$" → 's'
        var sut = Build(new List<string> { "silly" });

        var results = sut.Scan("what a $illy thing", "App");

        Assert.Single(results);
    }

    // ── Context snippet ────────────────────────────────────────────────────

    [Fact]
    public void Scan_ContextSnippet_ContainsMatchedText()
    {
        var sut = Build(new List<string> { "badword" });

        var results = sut.Scan("some text with badword in context", "App");

        Assert.Single(results);
        Assert.Contains("badword", results[0].ContextSnippet);
    }

    [Fact]
    public void Scan_ContextSnippet_MaxLengthIs120()
    {
        var sut = Build(new List<string> { "bad" });
        // Surround with 100 chars each side to force truncation (radius=40, but max=120)
        var longText = new string('x', 100) + " bad " + new string('y', 100);

        var results = sut.Scan(longText, "App");

        Assert.Single(results);
        Assert.True(results[0].ContextSnippet.Length <= 120,
            $"Snippet length {results[0].ContextSnippet.Length} exceeds 120");
    }

    [Fact]
    public void Scan_ContextSnippet_ForShortText_IsEntireText()
    {
        var sut = Build(new List<string> { "bad" });
        const string text = "this bad text";

        var results = sut.Scan(text, "App");

        Assert.Single(results);
        Assert.Equal(text, results[0].ContextSnippet);
    }

    // ── Word list deduplication ────────────────────────────────────────────

    [Fact]
    public void Scan_DeduplicatesWordList_NoDuplicateMatches()
    {
        // Providing "bad" three times should still produce one match per occurrence
        var sut = Build(new List<string> { "bad", "bad", "bad" });

        var results = sut.Scan("this is bad", "App");

        // Word list is deduplicated in RefreshWordList — only one entry for "bad"
        Assert.Single(results);
    }

    // ── Unicode / robustness ────────────────────────────────────────────────

    [Fact]
    public void Scan_HandlesUnicodeText_WithoutThrowing()
    {
        var sut = Build(new List<string> { "bad" });

        var ex = Record.Exception(() => sut.Scan("héllo wörld 🎉 with unicode", "App"));

        Assert.Null(ex);
    }

    [Fact]
    public void Scan_ReturnsEmpty_ForNullEquivalentInput_Whitespace()
    {
        var sut = Build(new List<string> { "bad" });

        Assert.Empty(sut.Scan("\t\n\r", "App"));
    }

    // ── Mixed case word list normalisation ─────────────────────────────────

    [Fact]
    public void Scan_WordListIsNormalisedToLowercase()
    {
        // Word list entry "BAD" should still match lowercase "bad" in text
        var sut = Build(new List<string> { "BAD" });

        var results = sut.Scan("this is bad text", "App");

        Assert.Single(results);
        Assert.Equal("bad", results[0].MatchedTerm);
    }
}
