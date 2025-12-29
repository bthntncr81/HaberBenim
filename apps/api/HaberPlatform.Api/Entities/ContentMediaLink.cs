namespace HaberPlatform.Api.Entities;

/// <summary>
/// Link table between ContentItem and MediaAsset
/// </summary>
public class ContentMediaLink
{
    public Guid Id { get; set; }
    
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
    
    /// <summary>Whether this is the primary/featured image for the content</summary>
    public bool IsPrimary { get; set; } = false;
    
    /// <summary>Sort order for displaying multiple images</summary>
    public int SortOrder { get; set; } = 0;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

