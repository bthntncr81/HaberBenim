namespace HaberPlatform.Api.Entities;

public class ContentDuplicate
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    public Guid DuplicateOfContentItemId { get; set; }
    public ContentItem DuplicateOfContentItem { get; set; } = null!;
    
    public required string Method { get; set; } // "url", "hash", "external_id"
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}


