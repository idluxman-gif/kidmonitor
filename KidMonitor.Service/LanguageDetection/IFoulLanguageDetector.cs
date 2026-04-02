namespace KidMonitor.Service.LanguageDetection;

/// <summary>
/// Scans a text fragment for foul/inappropriate language.
/// </summary>
public interface IFoulLanguageDetector
{
    /// <summary>
    /// Returns all matches found in <paramref name="text"/>.
    /// Returns an empty list when the text is clean or detection is disabled.
    /// </summary>
    IReadOnlyList<DetectionMatch> Scan(string text, string appName);
}

/// <param name="MatchedTerm">Normalised form of the word that triggered the match.</param>
/// <param name="ContextSnippet">Short surrounding text fragment (≤ 120 chars).</param>
public record DetectionMatch(string MatchedTerm, string ContextSnippet);
