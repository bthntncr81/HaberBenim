namespace HaberPlatform.Api.Entities;

public class ContentMedia
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    public required string MediaType { get; set; } // "image", "video", "audio"
    public required string Url { get; set; }
    public string? ThumbUrl { get; set; }
    public string? Title { get; set; }
    public long? SizeBytes { get; set; }
}


