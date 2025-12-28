using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services;

public class RssIngestionResult
{
    public int SourcesProcessed { get; set; }
    public int ItemsInserted { get; set; }
    public int DuplicatesFound { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    
    // Decision counts
    public Dictionary<string, int> ByDecisionTypeCounts { get; set; } = new();
}

public class RssIngestionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssIngestionService> _logger;

    public RssIngestionService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<RssIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<RssIngestionResult> IngestAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new RssIngestionResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ruleEngine = scope.ServiceProvider.GetRequiredService<RuleEngineService>();

        var sources = await db.Sources
            .Where(s => s.Type == "RSS" && s.IsActive && s.Url != null)
            .ToListAsync(cancellationToken);

        var alertService = scope.ServiceProvider.GetRequiredService<AlertService>();

        foreach (var source in sources)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var sourceResult = await IngestSourceAsync(db, ruleEngine, source, cancellationToken);
                result.SourcesProcessed++;
                result.ItemsInserted += sourceResult.ItemsInserted;
                result.DuplicatesFound += sourceResult.DuplicatesFound;
                
                // Merge decision type counts
                foreach (var kvp in sourceResult.ByDecisionTypeCounts)
                {
                    if (!result.ByDecisionTypeCounts.ContainsKey(kvp.Key))
                        result.ByDecisionTypeCounts[kvp.Key] = 0;
                    result.ByDecisionTypeCounts[kvp.Key] += kvp.Value;
                }

                // Update last fetched time
                source.LastFetchedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                // Update health status - success
                await alertService.UpdateSourceHealthAsync(source.Id, success: true);
            }
            catch (Exception ex)
            {
                result.Errors++;
                result.ErrorMessages.Add($"{source.Name}: {ex.Message}");
                _logger.LogError(ex, "Failed to ingest source {SourceName} ({SourceId})", source.Name, source.Id);

                // Update health status - failure
                await alertService.UpdateSourceHealthAsync(source.Id, success: false, error: ex.Message);
            }
        }

        // Check for failover conditions (X sources down)
        await alertService.CheckFailoverConditionsAsync();

        _logger.LogInformation(
            "RSS ingestion completed: {Sources} sources, {Items} items inserted, {Duplicates} duplicates, {Errors} errors",
            result.SourcesProcessed, result.ItemsInserted, result.DuplicatesFound, result.Errors);

        return result;
    }

    private async Task<RssIngestionResult> IngestSourceAsync(
        AppDbContext db, 
        RuleEngineService ruleEngine,
        Source source, 
        CancellationToken cancellationToken)
    {
        var result = new RssIngestionResult();

        var feed = await FetchFeedAsync(source.Url!, cancellationToken);
        if (feed == null) return result;

        foreach (var item in feed.Items)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var processed = await ProcessFeedItemAsync(db, ruleEngine, source, item, cancellationToken);
                if (processed.IsNew)
                {
                    result.ItemsInserted++;
                    
                    // Track decision type
                    if (processed.DecisionType != null)
                    {
                        if (!result.ByDecisionTypeCounts.ContainsKey(processed.DecisionType))
                            result.ByDecisionTypeCounts[processed.DecisionType] = 0;
                        result.ByDecisionTypeCounts[processed.DecisionType]++;
                    }
                }
                else if (processed.IsDuplicate)
                {
                    result.DuplicatesFound++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process item from {Source}: {Title}", 
                    source.Name, item.Title?.Text);
            }
        }

        return result;
    }

    private async Task<SyndicationFeed?> FetchFeedAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RssFetcher");
            client.Timeout = TimeSpan.FromSeconds(30);

            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                Async = true
            });

            return SyndicationFeed.Load(reader);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch RSS feed: {Url}", url);
            return null;
        }
    }

    private async Task<(bool IsNew, bool IsDuplicate, string? DecisionType)> ProcessFeedItemAsync(
        AppDbContext db,
        RuleEngineService ruleEngine,
        Source source,
        SyndicationItem item,
        CancellationToken cancellationToken)
    {
        // Extract data from feed item
        var externalId = GetExternalId(item);
        var canonicalUrl = GetCanonicalUrl(item);
        var title = item.Title?.Text ?? "";
        var summary = StripHtml(item.Summary?.Text);
        var originalText = item.Summary?.Text ?? item.Content?.ToString();
        var bodyText = NormalizeText(summary ?? title);
        var publishedAt = item.PublishDate.UtcDateTime;
        if (publishedAt == DateTime.MinValue) publishedAt = DateTime.UtcNow;

        var dedupHash = ComputeDedupHash(NormalizeText(title), canonicalUrl, publishedAt);

        // Check for duplicate by (SourceId, ExternalId)
        var existsByExternalId = await db.ContentItems
            .AnyAsync(c => c.SourceId == source.Id && c.ExternalId == externalId, cancellationToken);
        
        if (existsByExternalId)
        {
            return (false, true, null);
        }

        // Check for duplicate by CanonicalUrl
        if (!string.IsNullOrEmpty(canonicalUrl))
        {
            var existingByUrl = await db.ContentItems
                .FirstOrDefaultAsync(c => c.CanonicalUrl == canonicalUrl, cancellationToken);

            if (existingByUrl != null)
            {
                await CreateDuplicateRecord(db, source, externalId, existingByUrl, "url", cancellationToken);
                return (false, true, null);
            }
        }

        // Check for duplicate by DedupHash
        var existingByHash = await db.ContentItems
            .FirstOrDefaultAsync(c => c.DedupHash == dedupHash, cancellationToken);

        if (existingByHash != null)
        {
            await CreateDuplicateRecord(db, source, externalId, existingByHash, "hash", cancellationToken);
            return (false, true, null);
        }

        // Create new content item
        var contentItem = new ContentItem
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            ExternalId = externalId,
            Title = title.Length > 1000 ? title[..1000] : title,
            Summary = summary?.Length > 5000 ? summary[..5000] : summary,
            BodyText = bodyText,
            OriginalText = originalText,
            CanonicalUrl = canonicalUrl,
            PublishedAtUtc = publishedAt,
            IngestedAtUtc = DateTime.UtcNow,
            DedupHash = dedupHash,
            Status = ContentStatuses.New
        };

        // Add media from enclosures
        foreach (var link in item.Links.Where(l => l.RelationshipType == "enclosure"))
        {
            var mediaType = DetermineMediaType(link.MediaType);
            if (mediaType != null)
            {
                contentItem.Media.Add(new ContentMedia
                {
                    Id = Guid.NewGuid(),
                    ContentItemId = contentItem.Id,
                    MediaType = mediaType,
                    Url = link.Uri?.ToString() ?? "",
                    SizeBytes = link.Length
                });
            }
        }

        db.ContentItems.Add(contentItem);
        
        // Run rule engine to determine decision
        var decision = await ruleEngine.EvaluateAsync(contentItem, source);
        ruleEngine.ApplyDecision(contentItem, source, decision);
        
        // Create default draft for new content item
        var draft = CreateDefaultDraft(contentItem);
        db.ContentDrafts.Add(draft);
        
        await db.SaveChangesAsync(cancellationToken);

        return (true, false, decision.DecisionType);
    }

    private async Task CreateDuplicateRecord(
        AppDbContext db,
        Source source,
        string externalId,
        ContentItem original,
        string method,
        CancellationToken cancellationToken)
    {
        // Create a minimal content item to track the duplicate
        var duplicateItem = new ContentItem
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            ExternalId = externalId,
            Title = $"[DUP] {original.Title}",
            BodyText = original.BodyText,
            PublishedAtUtc = DateTime.UtcNow,
            IngestedAtUtc = DateTime.UtcNow,
            DedupHash = Guid.NewGuid().ToString("N"), // Unique to avoid constraint
            Status = ContentStatuses.Duplicate
        };

        var duplicateRecord = new ContentDuplicate
        {
            Id = Guid.NewGuid(),
            ContentItemId = duplicateItem.Id,
            DuplicateOfContentItemId = original.Id,
            Method = method,
            DetectedAtUtc = DateTime.UtcNow
        };

        // Increment duplicate count on original
        original.DuplicateCount++;

        db.ContentItems.Add(duplicateItem);
        db.ContentDuplicates.Add(duplicateRecord);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string GetExternalId(SyndicationItem item)
    {
        if (!string.IsNullOrEmpty(item.Id))
            return item.Id;

        var link = item.Links.FirstOrDefault(l => l.RelationshipType == "alternate")
                   ?? item.Links.FirstOrDefault();
        
        return link?.Uri?.ToString() ?? Guid.NewGuid().ToString();
    }

    private static string? GetCanonicalUrl(SyndicationItem item)
    {
        var link = item.Links.FirstOrDefault(l => l.RelationshipType == "alternate")
                   ?? item.Links.FirstOrDefault(l => l.MediaType == null || l.MediaType.Contains("html"));

        return link?.Uri?.ToString();
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;

        // Remove HTML tags
        var text = Regex.Replace(html, "<[^>]+>", " ");
        // Decode HTML entities
        text = HttpUtility.HtmlDecode(text);
        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Lowercase, remove special chars, collapse whitespace
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    private static string ComputeDedupHash(string normalizedTitle, string? canonicalUrl, DateTime publishedAt)
    {
        var input = $"{normalizedTitle}|{canonicalUrl ?? ""}|{publishedAt:yyyyMMddHHmm}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? DetermineMediaType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType)) return null;

        if (mimeType.StartsWith("image/")) return "image";
        if (mimeType.StartsWith("video/")) return "video";
        if (mimeType.StartsWith("audio/")) return "audio";

        return null;
    }

    private static ContentDraft CreateDefaultDraft(ContentItem item)
    {
        return new ContentDraft
        {
            Id = Guid.NewGuid(),
            ContentItemId = item.Id,
            XText = TruncateText(item.Title, 280),
            WebTitle = item.Title,
            WebBody = item.BodyText,
            MobileSummary = TruncateText(item.Summary ?? item.BodyText, 200),
            PushTitle = TruncateText(item.Title, 100),
            PushBody = TruncateText(item.Summary ?? item.BodyText, 200),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
