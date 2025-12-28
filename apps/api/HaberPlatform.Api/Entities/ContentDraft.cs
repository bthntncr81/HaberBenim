namespace HaberPlatform.Api.Entities;

/// <summary>
/// Editorial draft for a content item (1:1 relationship)
/// </summary>
public class ContentDraft
{
    public Guid Id { get; set; }
    
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    // Platform-specific text versions
    public string? XText { get; set; } // Twitter/X text (280 char limit)
    public string? WebTitle { get; set; }
    public string? WebBody { get; set; }
    public string? MobileSummary { get; set; }
    public string? PushTitle { get; set; }
    public string? PushBody { get; set; }
    
    // Social media fields
    public string? HashtagsCsv { get; set; }
    public string? MentionsCsv { get; set; }
    
    // Channel publishing toggles
    public bool PublishToWeb { get; set; } = true;
    public bool PublishToMobile { get; set; } = true;
    public bool PublishToX { get; set; } = true;
    
    // Metadata
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
