using System.Text.RegularExpressions;

namespace HaberPlatform.Api.Services;

/// <summary>
/// Cleans boilerplate phrases and normalizes content from RSS feeds
/// </summary>
public class ContentCleaner
{
    private static readonly string[] TruncationPhrases = 
    {
        "devamı için tıklayınız",
        "devamı için tıklayiniz",
        "devamı için tıklayınız...",
        "devamı için tıklayiniz...",
        "devamı için tıklayın",
        "devamı için tıklayin",
        "devami için tiklayiniz",
        "devami icin tiklayiniz",
        "devamı...",
        "devami...",
        "read more",
        "read more...",
        "continue reading",
        "continue reading..."
    };

    private static readonly Regex TrailingEllipsisPattern = new(@"\s*\.{3,}\s*$", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesPattern = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex TruncationPhrasePattern;

    static ContentCleaner()
    {
        var escapedPhrases = TruncationPhrases.Select(Regex.Escape);
        var pattern = $@"({string.Join("|", escapedPhrases)})\s*\.{{0,3}}\s*$";
        TruncationPhrasePattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    /// <summary>
    /// Detects if the content appears to be truncated
    /// </summary>
    public static bool DetectTruncation(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        var lowerContent = content.ToLowerInvariant();

        // Rule 1: Contains common truncation phrases
        foreach (var phrase in TruncationPhrases)
        {
            if (lowerContent.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Rule 2: Short content (< 300 chars) ending with "..."
        if (content.Length < 300 && content.TrimEnd().EndsWith("..."))
            return true;

        // Rule 3: Content ends with ellipsis in last 20 chars (teaser pattern)
        var last20 = content.Length > 20 ? content[^20..] : content;
        if (TrailingEllipsisPattern.IsMatch(last20) && content.Length < 500)
            return true;

        return false;
    }

    /// <summary>
    /// Cleans boilerplate phrases and normalizes content
    /// </summary>
    public static string Clean(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var result = content;

        // Remove truncation phrases
        result = TruncationPhrasePattern.Replace(result, "");

        // Remove trailing ellipsis if it's clearly a teaser cutoff
        if (result.TrimEnd().EndsWith("...") && result.Length < 500)
        {
            result = TrailingEllipsisPattern.Replace(result, "");
        }

        // Normalize whitespace
        result = MultipleSpacesPattern.Replace(result, " ");
        result = result.Trim();

        return result;
    }

    /// <summary>
    /// Cleans and returns both cleaned content and whether cleaning was applied
    /// </summary>
    public static (string CleanedContent, bool WasCleaned) CleanWithInfo(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (string.Empty, false);

        var cleaned = Clean(content);
        var wasCleaned = cleaned != content.Trim();

        return (cleaned, wasCleaned);
    }
}
