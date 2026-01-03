namespace HaberPlatform.Api.Entities;

/// <summary>
/// Emergency queue for high-priority breaking news content
/// </summary>
public class EmergencyQueueItem
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    
    /// <summary>
    /// Priority score (higher = more urgent)
    /// </summary>
    public int Priority { get; set; } = 100;
    
    /// <summary>
    /// Status: Pending, Publishing, Published, Cancelled
    /// </summary>
    public string Status { get; set; } = EmergencyQueueStatus.Pending;
    
    /// <summary>
    /// Keywords that triggered emergency detection (CSV)
    /// </summary>
    public string? MatchedKeywordsCsv { get; set; }
    
    /// <summary>
    /// Detection reason (keyword match, category, manual, etc.)
    /// </summary>
    public string? DetectionReason { get; set; }
    
    /// <summary>
    /// Platforms to publish to (CSV: Instagram,X,Web)
    /// </summary>
    public string? TargetPlatformsCsv { get; set; }
    
    /// <summary>
    /// Override night mode / schedule restrictions
    /// </summary>
    public bool OverrideSchedule { get; set; } = true;
    
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    
    /// <summary>
    /// User who approved/published (optional)
    /// </summary>
    public Guid? ProcessedByUserId { get; set; }
    
    // Navigation
    public ContentItem? ContentItem { get; set; }
    public User? ProcessedByUser { get; set; }
    
    // Helpers
    public List<string> MatchedKeywords => 
        string.IsNullOrEmpty(MatchedKeywordsCsv) 
            ? new List<string>() 
            : MatchedKeywordsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    
    public List<string> TargetPlatforms =>
        string.IsNullOrEmpty(TargetPlatformsCsv)
            ? new List<string> { "Instagram", "X", "Web", "Mobile" }
            : TargetPlatformsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
}

public static class EmergencyQueueStatus
{
    public const string Pending = "Pending";
    public const string Publishing = "Publishing";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";
    
    public static readonly string[] All = { Pending, Publishing, Published, Cancelled };
}

