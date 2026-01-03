namespace HaberPlatform.Api.Entities;

public class PublishTemplate
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    
    /// <summary>Instagram, X, TikTok, YouTube</summary>
    public required string Platform { get; set; }
    
    /// <summary>Post, Reels, Shorts, Video, Tweet, QuoteTweet</summary>
    public required string Format { get; set; }
    
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    
    /// <summary>JSON rule for when this template applies</summary>
    public string? RuleJson { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public PublishTemplateSpec? Spec { get; set; }
}

public static class TemplatePlatforms
{
    public const string Instagram = "Instagram";
    public const string X = "X";
    public const string TikTok = "TikTok";
    public const string YouTube = "YouTube";
    
    public static readonly string[] All = { Instagram, X, TikTok, YouTube };
}

public static class TemplateFormats
{
    public const string Post = "Post";
    public const string Reels = "Reels";
    public const string Shorts = "Shorts";
    public const string Video = "Video";
    public const string Tweet = "Tweet";
    public const string QuoteTweet = "QuoteTweet";
    
    public static readonly string[] All = { Post, Reels, Shorts, Video, Tweet, QuoteTweet };
}

