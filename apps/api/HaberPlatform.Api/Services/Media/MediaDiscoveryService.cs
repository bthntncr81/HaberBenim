using System.Text.RegularExpressions;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Media;

/// <summary>
/// Discovers media (images) from content sources (X tweets, RSS, OG tags)
/// </summary>
public partial class MediaDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MediaDiscoveryService> _logger;

    public MediaDiscoveryService(
        IHttpClientFactory httpClientFactory,
        ILogger<MediaDiscoveryService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("MediaDiscovery");
        _logger = logger;
    }

    /// <summary>
    /// Discover media candidates from content item based on its source type
    /// </summary>
    public async Task<List<MediaCandidate>> DiscoverAsync(
        ContentItem item, 
        Source source,
        string? rawPayload = null,
        CancellationToken ct = default)
    {
        var candidates = new List<MediaCandidate>();

        try
        {
            switch (source.Type)
            {
                case SourceTypes.X:
                    candidates.AddRange(DiscoverFromXTweet(item, rawPayload));
                    break;

                case SourceTypes.RSS:
                    candidates.AddRange(DiscoverFromRssItem(item));
                    // If no enclosure media, try OG image from canonical URL
                    if (candidates.Count == 0 && !string.IsNullOrEmpty(item.CanonicalUrl))
                    {
                        var ogCandidate = await DiscoverOgImageAsync(item.CanonicalUrl, ct);
                        if (ogCandidate != null)
                        {
                            candidates.Add(ogCandidate);
                        }
                    }
                    break;

                case SourceTypes.Manual:
                case SourceTypes.GoogleNews:
                    // Try OG image if we have a URL
                    if (!string.IsNullOrEmpty(item.CanonicalUrl))
                    {
                        var ogCandidate = await DiscoverOgImageAsync(item.CanonicalUrl, ct);
                        if (ogCandidate != null)
                        {
                            candidates.Add(ogCandidate);
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover media for content {ContentId}", item.Id);
        }

        return candidates;
    }

    /// <summary>
    /// Extract media from X tweet data (stored in OriginalText or parsed from URL patterns)
    /// </summary>
    private List<MediaCandidate> DiscoverFromXTweet(ContentItem item, string? rawPayload)
    {
        var candidates = new List<MediaCandidate>();

        // Try to parse tweet media from raw payload (if stored as JSON)
        if (!string.IsNullOrEmpty(rawPayload))
        {
            // Look for media URLs in tweet JSON
            // Pattern: "url":"https://pbs.twimg.com/media/..."
            var mediaMatches = TweetMediaUrlRegex().Matches(rawPayload);
            foreach (Match match in mediaMatches)
            {
                var url = match.Groups[1].Value;
                if (IsValidImageUrl(url))
                {
                    candidates.Add(new MediaCandidate(
                        Url: url,
                        AltText: null,
                        Origin: MediaOrigins.X
                    ));
                }
            }

            // Look for preview_image_url
            var previewMatches = PreviewImageUrlRegex().Matches(rawPayload);
            foreach (Match match in previewMatches)
            {
                var url = match.Groups[1].Value;
                if (IsValidImageUrl(url))
                {
                    candidates.Add(new MediaCandidate(
                        Url: url,
                        AltText: null,
                        Origin: MediaOrigins.X
                    ));
                }
            }
        }

        // Also check OriginalText for embedded t.co links that might expand to media
        if (!string.IsNullOrEmpty(item.OriginalText))
        {
            var urlMatches = HttpsUrlRegex().Matches(item.OriginalText);
            foreach (Match match in urlMatches)
            {
                var url = match.Value;
                if (url.Contains("pbs.twimg.com") || url.Contains("video.twimg.com"))
                {
                    candidates.Add(new MediaCandidate(
                        Url: url,
                        AltText: null,
                        Origin: MediaOrigins.X
                    ));
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Extract media from RSS item (enclosures or media:content stored in ContentMedia)
    /// </summary>
    private List<MediaCandidate> DiscoverFromRssItem(ContentItem item)
    {
        var candidates = new List<MediaCandidate>();

        // Use already-parsed ContentMedia entries
        foreach (var media in item.Media)
        {
            if (media.MediaType == "image" && !string.IsNullOrEmpty(media.Url))
            {
                candidates.Add(new MediaCandidate(
                    Url: media.Url,
                    AltText: media.Title,
                    Origin: MediaOrigins.RSS
                ));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Fetch OG image from article URL
    /// </summary>
    public async Task<MediaCandidate?> DiscoverOgImageAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "HaberPlatform/1.0 (Media Discovery Bot)");
            
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.StartsWith("text/html"))
            {
                return null;
            }

            // Read only first 64KB to find OG tags
            var buffer = new byte[65536];
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var bytesRead = await stream.ReadAsync(buffer, ct);
            var html = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Parse og:image
            var ogImageUrl = ExtractOgImage(html);
            if (!string.IsNullOrEmpty(ogImageUrl))
            {
                // Resolve relative URLs
                if (ogImageUrl.StartsWith("//"))
                {
                    ogImageUrl = "https:" + ogImageUrl;
                }
                else if (ogImageUrl.StartsWith("/"))
                {
                    var uri = new Uri(url);
                    ogImageUrl = $"{uri.Scheme}://{uri.Host}{ogImageUrl}";
                }

                return new MediaCandidate(
                    Url: ogImageUrl,
                    AltText: ExtractOgTitle(html),
                    Origin: MediaOrigins.OG
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch OG image from {Url}", url);
        }

        return null;
    }

    /// <summary>
    /// Extract og:image content from HTML
    /// </summary>
    private static string? ExtractOgImage(string html)
    {
        // Match <meta property="og:image" content="...">
        var match = OgImageRegex().Match(html);
        if (match.Success)
        {
            return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        // Also try twitter:image
        match = TwitterImageRegex().Match(html);
        if (match.Success)
        {
            return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        return null;
    }

    /// <summary>
    /// Extract og:title for alt text
    /// </summary>
    private static string? ExtractOgTitle(string html)
    {
        var match = OgTitleRegex().Match(html);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static bool IsValidImageUrl(string url)
    {
        return !string.IsNullOrEmpty(url) && 
               (url.StartsWith("http://") || url.StartsWith("https://"));
    }

    // Compiled regex patterns
    [GeneratedRegex(@"""url""\s*:\s*""(https://pbs\.twimg\.com/media/[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex TweetMediaUrlRegex();

    [GeneratedRegex(@"""preview_image_url""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PreviewImageUrlRegex();

    [GeneratedRegex(@"https://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex HttpsUrlRegex();

    [GeneratedRegex(@"<meta\s+[^>]*property\s*=\s*[""']og:image[""'][^>]*content\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OgImageRegex();

    [GeneratedRegex(@"<meta\s+[^>]*name\s*=\s*[""']twitter:image[""'][^>]*content\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TwitterImageRegex();

    [GeneratedRegex(@"<meta\s+[^>]*property\s*=\s*[""']og:title[""'][^>]*content\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OgTitleRegex();
}

