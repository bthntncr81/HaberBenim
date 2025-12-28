using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Utils;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Publishes content to the web platform (upserts PublishedContent)
/// </summary>
public class WebPublisher : IChannelPublisher
{
    private readonly AppDbContext _db;
    private readonly ILogger<WebPublisher> _logger;

    public string ChannelName => PublishChannels.Web;

    public WebPublisher(AppDbContext db, ILogger<WebPublisher> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(ContentItem item, ContentDraft draft, CancellationToken ct = default)
    {
        try
        {
            var webTitle = draft.WebTitle ?? item.Title;
            var webBody = draft.WebBody ?? item.BodyText;
            
            // Generate slug from title
            var slug = SlugHelper.GenerateSlug(webTitle);

            // Build source attribution for compliance
            var attribution = BuildSourceAttribution(item.Source);

            var requestPayload = new
            {
                contentItemId = item.Id,
                webTitle,
                webBody,
                canonicalUrl = item.CanonicalUrl,
                sourceName = item.Source?.Name,
                categoryOrGroup = item.Source?.Group,
                slug,
                sourceAttribution = attribution
            };

            var requestJson = JsonSerializer.Serialize(requestPayload);

            // Upsert PublishedContent
            var existing = await _db.PublishedContents
                .FirstOrDefaultAsync(p => p.ContentItemId == item.Id, ct);

            if (existing != null)
            {
                existing.WebTitle = webTitle;
                existing.WebBody = webBody;
                existing.CanonicalUrl = item.CanonicalUrl;
                existing.SourceName = item.Source?.Name;
                existing.CategoryOrGroup = item.Source?.Group;
                existing.Slug = slug;
                existing.Path = SlugHelper.GeneratePath(existing.Id, slug);
                existing.SourceAttributionText = attribution;
                existing.PublishedAtUtc = DateTime.UtcNow;
            }
            else
            {
                var id = Guid.NewGuid();
                var published = new PublishedContent
                {
                    Id = id,
                    ContentItemId = item.Id,
                    WebTitle = webTitle,
                    WebBody = webBody,
                    CanonicalUrl = item.CanonicalUrl,
                    SourceName = item.Source?.Name,
                    CategoryOrGroup = item.Source?.Group,
                    Slug = slug,
                    Path = SlugHelper.GeneratePath(id, slug),
                    SourceAttributionText = attribution,
                    PublishedAtUtc = DateTime.UtcNow
                };
                _db.PublishedContents.Add(published);
                existing = published;
            }

            await _db.SaveChangesAsync(ct);

            var responsePayload = new
            {
                publishedContentId = existing.Id,
                slug = existing.Slug,
                path = existing.Path,
                publishedAt = existing.PublishedAtUtc
            };

            _logger.LogInformation("Published content {ContentId} to Web with path {Path}", item.Id, existing.Path);

            return PublishResult.Succeeded(requestJson, JsonSerializer.Serialize(responsePayload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish content {ContentId} to Web", item.Id);
            return PublishResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Build source attribution text for compliance
    /// </summary>
    private static string BuildSourceAttribution(Source? source)
    {
        if (source == null)
            return "Kaynak: Bilinmiyor";

        // For X type sources, try to extract handle from URL
        if (source.Type == "X" && !string.IsNullOrEmpty(source.Url))
        {
            // Try to extract @handle from X/Twitter URL
            var urlParts = source.Url.Split('/');
            var handle = urlParts.LastOrDefault(p => !string.IsNullOrEmpty(p) && !p.Contains('.'));
            if (!string.IsNullOrEmpty(handle))
            {
                return $"Kaynak: @{handle}";
            }
        }

        // For RSS and other sources
        return $"Kaynak: {source.Name}";
    }
}
