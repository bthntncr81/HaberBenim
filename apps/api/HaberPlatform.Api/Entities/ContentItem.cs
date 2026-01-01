namespace HaberPlatform.Api.Entities;

public class ContentItem
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    
    public required string ExternalId { get; set; } // RSS item.Guid or link
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public required string BodyText { get; set; } // normalized plain text
    public string? OriginalText { get; set; } // raw HTML
    public string? CanonicalUrl { get; set; }
    public string? Language { get; set; }
    
    public DateTime PublishedAtUtc { get; set; }
    public DateTime IngestedAtUtc { get; set; } = DateTime.UtcNow;
    
    public required string DedupHash { get; set; } // SHA256 for deduplication
    public int DuplicateCount { get; set; } = 0;
    
    // Status: New, PendingApproval, Blocked, Scheduled, AutoReady, ReadyToPublish, Rejected, Published, Archived, Duplicate
    public string Status { get; set; } = "New";
    
    // Decision fields (from rule engine)
    public string? DecisionType { get; set; } // AutoPublish, RequireApproval, Block, Schedule
    public Guid? DecidedByRuleId { get; set; }
    public Rule? DecidedByRule { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public int? TrustLevelSnapshot { get; set; } // Copy from source at decision time

    // Editorial fields
    public string? EditorialNote { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? LastEditedByUserId { get; set; }
    public User? LastEditedByUser { get; set; }
    public DateTime? LastEditedAtUtc { get; set; }

    // Version-aware publishing (Sprint 7)
    public int CurrentVersionNo { get; set; } = 1;
    public Guid? PublishedByUserId { get; set; }
    public User? PublishedByUser { get; set; }
    public string? PublishOrigin { get; set; } // "Auto" or "Editorial"

    // Breaking News Mode (Sprint 8)
    public bool IsBreaking { get; set; } = false;
    public DateTime? BreakingAtUtc { get; set; }
    public Guid? BreakingByUserId { get; set; }
    public User? BreakingByUser { get; set; }
    public bool BreakingPushRequired { get; set; } = true;
    public string? BreakingNote { get; set; }
    public int BreakingPriority { get; set; } = 100;

    // Retract/Takedown (Sprint 8)
    public string? RetractReason { get; set; }
    public DateTime? RetractedAtUtc { get; set; }
    public Guid? RetractedByUserId { get; set; }
    public User? RetractedByUser { get; set; }

    // RSS Full-Text Enrichment (Sprint 12)
    /// <summary>Summary HTML from RSS description</summary>
    public string? SummaryHtml { get; set; }
    
    /// <summary>HTML content from RSS (content:encoded or description)</summary>
    public string? ContentHtml { get; set; }
    
    /// <summary>Plain text extracted from full article</summary>
    public string? ContentText { get; set; }
    
    /// <summary>Whether the original RSS content was truncated</summary>
    public bool IsTruncated { get; set; } = false;
    
    /// <summary>Error message if article fetch failed</summary>
    public string? ArticleFetchError { get; set; }

    // Navigation
    public ICollection<ContentMedia> Media { get; set; } = new List<ContentMedia>();
    public ICollection<ContentMediaLink> MediaLinks { get; set; } = new List<ContentMediaLink>();
    public ICollection<ContentDuplicate> Duplicates { get; set; } = new List<ContentDuplicate>();
    public ContentDraft? Draft { get; set; }
    public ICollection<ContentRevision> Revisions { get; set; } = new List<ContentRevision>();
    public PublishedContent? PublishedContent { get; set; }
    public ICollection<PublishJob> PublishJobs { get; set; } = new List<PublishJob>();
    public ICollection<ChannelPublishLog> PublishLogs { get; set; } = new List<ChannelPublishLog>();
}

public static class PublishOrigins
{
    public const string Auto = "Auto";
    public const string Editorial = "Editorial";
}
