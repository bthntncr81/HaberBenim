namespace HaberPlatform.Api.Entities;

/// <summary>
/// AI Video generation job using OpenAI Sora
/// </summary>
public class AiVideoJob
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "sora-2";
    public string Prompt { get; set; } = "";
    public string Seconds { get; set; } = "8";
    public string Size { get; set; } = "1280x720";
    
    public string Status { get; set; } = AiVideoJobStatus.Queued;
    public string? OpenAiVideoId { get; set; }
    public int Progress { get; set; } = 0;
    public string? Error { get; set; }
    
    public Guid? MediaAssetId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    
    // Navigation
    public ContentItem? ContentItem { get; set; }
    public MediaAsset? MediaAsset { get; set; }
}

public static class AiVideoJobStatus
{
    public const string Queued = "Queued";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public static class AiVideoMode
{
    public const string AutoPrompt = "AutoPrompt";
    public const string CustomPrompt = "CustomPrompt";
}

