namespace HaberPlatform.Api.Entities;

public class RenderJob
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    
    /// <summary>Instagram, X, TikTok, YouTube</summary>
    public required string Platform { get; set; }
    
    /// <summary>Post, Reels, Shorts, Video, Tweet, QuoteTweet</summary>
    public required string Format { get; set; }
    
    public Guid TemplateId { get; set; }
    
    /// <summary>Queued, Rendering, Completed, Failed</summary>
    public string Status { get; set; } = RenderJobStatus.Queued;
    
    /// <summary>Image or Video</summary>
    public string OutputType { get; set; } = RenderOutputTypes.Image;
    
    /// <summary>The rendered output media asset</summary>
    public Guid? OutputMediaAssetId { get; set; }
    
    /// <summary>Source video asset for video renders</summary>
    public Guid? SourceVideoAssetId { get; set; }
    
    /// <summary>Resolved text spec JSON for this render</summary>
    public string? ResolvedTextSpecJson { get; set; }
    
    /// <summary>Error message if failed</summary>
    public string? Error { get; set; }
    
    /// <summary>FFmpeg progress (0-100) for video renders</summary>
    public int Progress { get; set; } = 0;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    
    // Navigation
    public ContentItem? ContentItem { get; set; }
    public PublishTemplate? Template { get; set; }
    public MediaAsset? OutputMediaAsset { get; set; }
    public MediaAsset? SourceVideoAsset { get; set; }
}

public static class RenderJobStatus
{
    public const string Queued = "Queued";
    public const string Rendering = "Rendering";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    
    public static readonly string[] All = { Queued, Rendering, Completed, Failed };
}

public static class RenderOutputTypes
{
    public const string Image = "Image";
    public const string Video = "Video";
    
    public static readonly string[] All = { Image, Video };
}

public static class VideoFormats
{
    public const string Reels = "Reels";      // Instagram vertical video
    public const string Shorts = "Shorts";    // YouTube vertical video
    public const string Video = "Video";      // TikTok video
    
    public static readonly string[] All = { Reels, Shorts, Video };
    
    public static bool IsVideoFormat(string format) => 
        format == Reels || format == Shorts || format == Video;
    
    public static string GetFormatForPlatform(string platform) => platform switch
    {
        "Instagram" => Reels,
        "YouTube" => Shorts,
        "TikTok" => Video,
        _ => Video
    };
}

