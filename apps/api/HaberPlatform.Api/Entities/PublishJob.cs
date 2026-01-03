namespace HaberPlatform.Api.Entities;

/// <summary>
/// Publishing job for scheduled and immediate publishing
/// </summary>
public class PublishJob
{
    public Guid Id { get; set; }
    
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    public DateTime ScheduledAtUtc { get; set; }
    
    // Version to publish (Sprint 7)
    public int VersionNo { get; set; } = 1;
    
    // Status: Pending, Running, Succeeded, Failed, Cancelled
    public string Status { get; set; } = PublishJobStatuses.Pending;
    
    public int AttemptCount { get; set; } = 0;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public string? LastError { get; set; }
    
    /// <summary>
    /// Emergency content bypasses scheduling (Sprint 18)
    /// </summary>
    public bool IsEmergency { get; set; } = false;
    
    /// <summary>
    /// Target platforms for this job (CSV: Instagram,X,Web) (Sprint 18)
    /// </summary>
    public string? TargetPlatformsCsv { get; set; }
    
    /// <summary>
    /// Silence push notifications (night mode) (Sprint 18)
    /// </summary>
    public bool SilencePush { get; set; } = false;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    
    // Helpers
    public List<string> TargetPlatforms =>
        string.IsNullOrEmpty(TargetPlatformsCsv)
            ? new List<string>()
            : TargetPlatformsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
}

public static class PublishJobStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Completed = "Completed"; // Alias for Succeeded
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

