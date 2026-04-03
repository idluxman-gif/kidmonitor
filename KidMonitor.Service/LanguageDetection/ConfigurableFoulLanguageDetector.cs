using KidMonitor.Core.Configuration;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service.LanguageDetection;

/// <summary>
/// Text-based foul language detector backed by a configurable word list.
///
/// Supports fuzzy matching via l33tspeak normalisation so that obfuscations
/// such as "f4ck", "@ss", or "sh1t" are still detected.
/// </summary>
public class ConfigurableFoulLanguageDetector : IFoulLanguageDetector
{
    // Characters substituted back to their plain-letter equivalents before matching.
    private static readonly Dictionary<char, char> L33tMap = new()
    {
        ['4'] = 'a', ['@'] = 'a',
        ['3'] = 'e',
        ['1'] = 'i', ['!'] = 'i',
        ['0'] = 'o',
        ['5'] = 's', ['$'] = 's',
        ['7'] = 't',
        ['6'] = 'g',
        ['8'] = 'b',
        ['9'] = 'g',
        ['+'] = 't',
    };

    // Snippet window on each side of a match (characters).
    private const int SnippetRadius = 40;
    private const int MaxSnippetLength = 120;

    private readonly IOptionsMonitor<MonitoringOptions> _options;
    private readonly IOptionsMonitor<FoulLanguageOptions> _legacyOptions;
    private readonly ILogger<ConfigurableFoulLanguageDetector> _logger;

    // Cached word set; rebuilt when config changes.
    private volatile IReadOnlyList<string> _wordList = Array.Empty<string>();
    private string _wordListFingerprint = string.Empty;

    public ConfigurableFoulLanguageDetector(
        IOptionsMonitor<MonitoringOptions> options,
        IOptionsMonitor<FoulLanguageOptions> legacyOptions,
        ILogger<ConfigurableFoulLanguageDetector> logger)
    {
        _options = options;
        _legacyOptions = legacyOptions;
        _logger = logger;
        RefreshWordList();
        _options.OnChange(_ => RefreshWordList());
    }

    public IReadOnlyList<DetectionMatch> Scan(string text, string appName)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<DetectionMatch>();

        var opts = _options.CurrentValue.LanguageDetection;
        if (!opts.Enabled)
            return Array.Empty<DetectionMatch>();

        var words = _wordList;
        if (words.Count == 0)
            return Array.Empty<DetectionMatch>();

        var normalised = Normalise(text);
        var matches = new List<DetectionMatch>();

        foreach (var word in words)
        {
            var idx = 0;
            while (true)
            {
                var pos = normalised.IndexOf(word, idx, StringComparison.Ordinal);
                if (pos < 0)
                    break;

                // Word-boundary guard: don't flag substrings inside longer innocent words
                // (simple ASCII check — good enough for a word-list approach).
                var before = pos > 0 ? text[pos - 1] : ' ';
                var after = pos + word.Length < text.Length ? text[pos + word.Length] : ' ';

                if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
                {
                    var snippet = BuildSnippet(text, pos, word.Length);
                    matches.Add(new DetectionMatch(word, snippet));
                }

                idx = pos + 1;
            }
        }

        return matches;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void RefreshWordList()
    {
        var opts = _options.CurrentValue.LanguageDetection;
        var legacy = _legacyOptions.CurrentValue;

        List<string> words;

        if (opts.WordList.Count > 0)
        {
            words = opts.WordList
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.Length > 0)
                .Distinct()
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(legacy.WordListPath) && File.Exists(legacy.WordListPath))
        {
            words = File.ReadAllLines(legacy.WordListPath)
                .Select(l => l.Trim().ToLowerInvariant())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .Distinct()
                .ToList();
        }
        else
        {
            words = new List<string>();
            _logger.LogWarning(
                "No foul-language word list configured. " +
                "Set Monitoring:LanguageDetection:WordList or FoulLanguage:WordListPath.");
        }

        var fingerprint = string.Join(",", words);
        if (fingerprint == _wordListFingerprint)
            return;

        _wordList = words;
        _wordListFingerprint = fingerprint;
        _logger.LogInformation("Foul language word list loaded: {Count} entries.", words.Count);
    }

    /// <summary>
    /// Applies l33tspeak substitution and lowercases so the word list can be plain text.
    /// </summary>
    private static string Normalise(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = char.ToLowerInvariant(chars[i]);
            chars[i] = L33tMap.TryGetValue(c, out var mapped) ? mapped : c;
        }
        return new string(chars);
    }

    private static string BuildSnippet(string original, int matchPos, int matchLen)
    {
        var start = Math.Max(0, matchPos - SnippetRadius);
        var end = Math.Min(original.Length, matchPos + matchLen + SnippetRadius);
        var raw = original[start..end];
        return raw.Length > MaxSnippetLength ? raw[..MaxSnippetLength] : raw;
    }
}
