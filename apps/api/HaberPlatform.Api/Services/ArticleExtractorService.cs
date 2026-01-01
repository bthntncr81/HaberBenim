using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace HaberPlatform.Api.Services;

/// <summary>
/// Extraction modes for full-text article fetching
/// </summary>
public static class FullTextExtractModes
{
    public const string Auto = "Auto";
    public const string JsonLd = "JsonLd";
    public const string Readability = "Readability";
    public const string None = "None";

    public static readonly string[] All = { Auto, JsonLd, Readability, None };

    public static bool IsValid(string? mode) =>
        !string.IsNullOrEmpty(mode) && All.Contains(mode);
}

/// <summary>
/// Result of article extraction
/// </summary>
public class ArticleExtractionResult
{
    public bool Success { get; set; }
    public string? ContentHtml { get; set; }
    public string? ContentText { get; set; }
    public string? Summary { get; set; }
    public string? Error { get; set; }
    public string? ExtractionMethod { get; set; }
}

/// <summary>
/// Service to extract full article text from URLs
/// </summary>
public class ArticleExtractorService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ArticleExtractorService> _logger;

    private static readonly Regex JsonLdScriptPattern = new(
        @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OgDescriptionPattern = new(
        @"<meta[^>]*property\s*=\s*[""']og:description[""'][^>]*content\s*=\s*[""']([^""']*)[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OgDescriptionPatternAlt = new(
        @"<meta[^>]*content\s*=\s*[""']([^""']*)[""'][^>]*property\s*=\s*[""']og:description[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArticleTagPattern = new(
        @"<article[^>]*>(.*?)</article>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ParagraphPattern = new(
        @"<p[^>]*>(.*?)</p>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex HtmlTagPattern = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex ScriptStylePattern = new(
        @"<(script|style|noscript|iframe|nav|header|footer|aside)[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public ArticleExtractorService(
        IHttpClientFactory httpClientFactory,
        ILogger<ArticleExtractorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Extracts full article content from URL
    /// </summary>
    public async Task<ArticleExtractionResult> ExtractAsync(string articleUrl, string mode = "Auto")
    {
        if (mode == FullTextExtractModes.None)
        {
            return new ArticleExtractionResult { Success = false, Error = "Extraction disabled" };
        }

        try
        {
            var html = await FetchHtmlAsync(articleUrl);
            if (string.IsNullOrEmpty(html))
            {
                return new ArticleExtractionResult { Success = false, Error = "Failed to fetch article HTML" };
            }

            // Try extraction methods based on mode
            ArticleExtractionResult? result = null;

            if (mode == FullTextExtractModes.Auto || mode == FullTextExtractModes.JsonLd)
            {
                result = TryExtractJsonLd(html);
                if (result?.Success == true)
                {
                    result.ExtractionMethod = "JsonLd";
                    return result;
                }
            }

            // Try OG description as fallback summary
            var ogDescription = ExtractOgDescription(html);

            if (mode == FullTextExtractModes.Auto || mode == FullTextExtractModes.Readability)
            {
                result = TryExtractReadability(html);
                if (result?.Success == true)
                {
                    result.ExtractionMethod = "Readability";
                    result.Summary ??= ogDescription;
                    return result;
                }
            }

            // If we at least got OG description, use that
            if (!string.IsNullOrEmpty(ogDescription))
            {
                return new ArticleExtractionResult
                {
                    Success = true,
                    Summary = ogDescription,
                    ContentText = ogDescription,
                    ExtractionMethod = "OgDescription"
                };
            }

            return new ArticleExtractionResult { Success = false, Error = "No extractable content found" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract article from {Url}", articleUrl);
            return new ArticleExtractionResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<string?> FetchHtmlAsync(string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ArticleFetcher");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HaberPlatformBot/1.0 (+contact)");

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch HTML from {Url}", url);
            return null;
        }
    }

    private ArticleExtractionResult? TryExtractJsonLd(string html)
    {
        try
        {
            var matches = JsonLdScriptPattern.Matches(html);
            foreach (Match match in matches)
            {
                var jsonContent = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(jsonContent)) continue;

                try
                {
                    // Handle both single object and array formats
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;

                    // If it's an array, iterate through items
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                        {
                            var result = TryParseJsonLdItem(item);
                            if (result != null) return result;
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        // Check for @graph property
                        if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in graph.EnumerateArray())
                            {
                                var result = TryParseJsonLdItem(item);
                                if (result != null) return result;
                            }
                        }
                        else
                        {
                            var result = TryParseJsonLdItem(root);
                            if (result != null) return result;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Invalid JSON, try next match
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JSON-LD extraction failed");
        }

        return null;
    }

    private ArticleExtractionResult? TryParseJsonLdItem(JsonElement item)
    {
        // Check @type
        if (!item.TryGetProperty("@type", out var typeElement))
            return null;

        var type = typeElement.ValueKind == JsonValueKind.Array
            ? string.Join(",", typeElement.EnumerateArray().Select(t => t.GetString()))
            : typeElement.GetString();

        if (string.IsNullOrEmpty(type))
            return null;

        // Must be NewsArticle, Article, or similar
        var validTypes = new[] { "NewsArticle", "Article", "BlogPosting", "WebPage", "ReportageNewsArticle" };
        if (!validTypes.Any(vt => type.Contains(vt, StringComparison.OrdinalIgnoreCase)))
            return null;

        // Extract articleBody
        if (item.TryGetProperty("articleBody", out var articleBody))
        {
            var body = articleBody.GetString();
            if (!string.IsNullOrWhiteSpace(body))
            {
                var description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null;

                return new ArticleExtractionResult
                {
                    Success = true,
                    ContentText = ContentCleaner.Clean(body),
                    Summary = description,
                    ContentHtml = null // JSON-LD typically provides plain text
                };
            }
        }

        return null;
    }

    private string? ExtractOgDescription(string html)
    {
        var match = OgDescriptionPattern.Match(html);
        if (!match.Success)
        {
            match = OgDescriptionPatternAlt.Match(html);
        }

        if (match.Success)
        {
            var description = HttpUtility.HtmlDecode(match.Groups[1].Value);
            return ContentCleaner.Clean(description);
        }

        return null;
    }

    private ArticleExtractionResult? TryExtractReadability(string html)
    {
        try
        {
            // Remove scripts, styles, nav, etc.
            var cleanedHtml = ScriptStylePattern.Replace(html, " ");

            // Try to find <article> tag first
            var articleMatch = ArticleTagPattern.Match(cleanedHtml);
            string contentHtml;

            if (articleMatch.Success)
            {
                contentHtml = articleMatch.Groups[1].Value;
            }
            else
            {
                // Fallback: extract all paragraphs and find the best cluster
                contentHtml = ExtractBestParagraphCluster(cleanedHtml);
            }

            if (string.IsNullOrWhiteSpace(contentHtml))
                return null;

            // Convert to plain text
            var contentText = HtmlToPlainText(contentHtml);
            contentText = ContentCleaner.Clean(contentText);

            if (string.IsNullOrWhiteSpace(contentText) || contentText.Length < 100)
                return null;

            return new ArticleExtractionResult
            {
                Success = true,
                ContentHtml = contentHtml,
                ContentText = contentText
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Readability extraction failed");
            return null;
        }
    }

    private string ExtractBestParagraphCluster(string html)
    {
        var paragraphs = ParagraphPattern.Matches(html)
            .Select(m => m.Groups[1].Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => HtmlToPlainText(p))
            .Where(p => p.Length > 50) // Filter out short paragraphs
            .ToList();

        if (paragraphs.Count == 0)
            return string.Empty;

        // Return all substantial paragraphs joined
        return string.Join("\n\n", paragraphs);
    }

    private string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Remove HTML tags
        var text = HtmlTagPattern.Replace(html, " ");

        // Decode HTML entities
        text = HttpUtility.HtmlDecode(text);

        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}
