namespace HaberPlatform.Api.Entities;

/// <summary>
/// Log of publishing attempts per channel for idempotency
/// </summary>
public class ChannelPublishLog
{
    public Guid Id { get; set; }
    
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    // Channel: Web, Mobile, X
    public string Channel { get; set; } = string.Empty;
    
    // Version published (Sprint 7)
    public int VersionNo { get; set; } = 1;
    
    // Status: Success, Failed, Skipped
    public string Status { get; set; } = string.Empty;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? Error { get; set; }
    
    /// <summary>
    /// External ID from the platform (e.g., tweet ID, post ID)
    /// </summary>
    public string? ExternalPostId { get; set; }
}

public static class PublishChannels
{
    public const string Web = "Web";
    public const string Mobile = "Mobile";
    public const string X = "X";
    public const string Instagram = "Instagram";
    
    public static readonly string[] All = [Web, Mobile, X, Instagram];
}

public static class ChannelPublishStatuses
{
    public const string Success = "Success";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

