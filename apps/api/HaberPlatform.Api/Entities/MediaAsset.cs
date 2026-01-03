namespace HaberPlatform.Api.Entities;

/// <summary>
/// Represents a stored media file (image or video)
/// </summary>
public class MediaAsset
{
    public Guid Id { get; set; }
    
    /// <summary>Kind: "Image" or "Video"</summary>
    public required string Kind { get; set; }
    
    /// <summary>Origin: "X" | "RSS" | "OG" | "AI" | "Manual"</summary>
    public required string Origin { get; set; }
    
    /// <summary>Original URL the asset was downloaded from (if any)</summary>
    public string? SourceUrl { get; set; }
    
    /// <summary>Local storage path (relative to Media.RootDir)</summary>
    public required string StoragePath { get; set; }
    
    /// <summary>MIME type: image/jpeg, image/png, etc.</summary>
    public required string ContentType { get; set; }
    
    /// <summary>File size in bytes</summary>
    public long SizeBytes { get; set; }
    
    /// <summary>Image width in pixels</summary>
    public int Width { get; set; }
    
    /// <summary>Image height in pixels</summary>
    public int Height { get; set; }
    
    /// <summary>Video duration in seconds (for video assets)</summary>
    public int? DurationSeconds { get; set; }
    
    /// <summary>SHA256 hash of file contents for deduplication</summary>
    public string? Sha256 { get; set; }
    
    /// <summary>Alt text for accessibility</summary>
    public string? AltText { get; set; }
    
    /// <summary>AI generation prompt (if Origin=AI)</summary>
    public string? GenerationPrompt { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ICollection<ContentMediaLink> ContentLinks { get; set; } = new List<ContentMediaLink>();
}

/// <summary>Valid media asset kinds</summary>
public static class MediaKinds
{
    public const string Image = "Image";
    public const string Video = "Video";
}

/// <summary>Valid media origin types</summary>
public static class MediaOrigins
{
    public const string X = "X";
    public const string RSS = "RSS";
    public const string OG = "OG";
    public const string AI = "AI";
    public const string Manual = "Manual";
}

