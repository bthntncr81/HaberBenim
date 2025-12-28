namespace HaberPlatform.Api.Entities;

public class Rule
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0; // Higher wins
    
    // DecisionType: AutoPublish, RequireApproval, Block, Schedule
    public required string DecisionType { get; set; }
    
    // Matching criteria
    public int? MinTrustLevel { get; set; } // 1..3 or 0..100
    public string? KeywordsIncludeCsv { get; set; } // "deprem,son dakika,..."
    public string? KeywordsExcludeCsv { get; set; } // Keywords that block match
    public string? SourceIdsCsv { get; set; } // CSV of GUIDs
    public string? GroupIdsCsv { get; set; } // CSV of group names
    
    // Metadata
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    
    // Navigation
    public ICollection<ContentItem> DecidedContentItems { get; set; } = new List<ContentItem>();
}

// Decision type constants
public static class DecisionTypes
{
    public const string AutoPublish = "AutoPublish";
    public const string RequireApproval = "RequireApproval";
    public const string Block = "Block";
    public const string Schedule = "Schedule";
    
    public static readonly string[] All = [AutoPublish, RequireApproval, Block, Schedule];
}

// Content status constants
public static class ContentStatuses
{
    public const string New = "New";
    public const string PendingApproval = "PendingApproval";
    public const string Blocked = "Blocked";
    public const string Scheduled = "Scheduled";
    public const string AutoReady = "AutoReady";
    public const string ReadyToPublish = "ReadyToPublish";
    public const string Rejected = "Rejected";
    public const string Published = "Published";
    public const string Archived = "Archived";
    public const string Duplicate = "Duplicate";
    public const string Retracted = "Retracted"; // Sprint 8: Content takedown
}
