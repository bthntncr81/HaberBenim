namespace HaberPlatform.Api.Entities;

/// <summary>
/// Published content for web site consumption
/// </summary>
public class PublishedContent
{
    public Guid Id { get; set; }
    
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    public string WebTitle { get; set; } = string.Empty;
    public string WebBody { get; set; } = string.Empty;
    public string? CanonicalUrl { get; set; }
    public string? SourceName { get; set; }
    public string? CategoryOrGroup { get; set; }
    
    // SEO fields
    public string Slug { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;

    // Compliance (Sprint 8)
    public string? SourceAttributionText { get; set; }

    // Retract (Sprint 8)
    public bool IsRetracted { get; set; } = false;
    public DateTime? RetractedAtUtc { get; set; }

    // Media (Sprint 10)
    /// <summary>URL to primary image served from /media/...</summary>
    public string? PrimaryImageUrl { get; set; }
}
