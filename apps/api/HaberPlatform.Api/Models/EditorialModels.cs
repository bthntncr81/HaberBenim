using System.ComponentModel.DataAnnotations;

namespace HaberPlatform.Api.Models;

// Inbox list item DTO
public record EditorialInboxItemDto(
    Guid Id,
    DateTime PublishedAtUtc,
    string SourceName,
    string Title,
    string? Summary,
    string Status,
    string? DecisionType,
    DateTime? DecidedAtUtc,
    DateTime? ScheduledAtUtc,
    bool HasDraft
);

public record EditorialInboxResponse(
    List<EditorialInboxItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

// Full item detail DTO
public record EditorialItemDto(
    Guid Id,
    DateTime PublishedAtUtc,
    DateTime IngestedAtUtc,
    Guid SourceId,
    string SourceName,
    string Title,
    string? Summary,
    string BodyText,
    string? OriginalText,
    string? CanonicalUrl,
    string? Language,
    string Status,
    string? DecisionType,
    string? DecisionReason,
    DateTime? DecidedAtUtc,
    DateTime? ScheduledAtUtc,
    int? TrustLevelSnapshot,
    string? EditorialNote,
    string? RejectionReason,
    Guid? LastEditedByUserId,
    DateTime? LastEditedAtUtc,
    EditorialDraftDto? Draft,
    List<EditorialMediaDto> Media,
    List<EditorialRevisionDto> Revisions
);

public record EditorialDraftDto(
    Guid Id,
    string? XText,
    string? WebTitle,
    string? WebBody,
    string? MobileSummary,
    string? PushTitle,
    string? PushBody,
    string? HashtagsCsv,
    string? MentionsCsv,
    bool PublishToWeb,
    bool PublishToMobile,
    bool PublishToX,
    bool PublishToInstagram,
    string? InstagramCaptionOverride,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByUserId
);

public record EditorialMediaDto(
    Guid Id,
    string MediaType,
    string Url,
    string? ThumbUrl
);

public record EditorialRevisionDto(
    Guid Id,
    int VersionNo,
    string ActionType,
    DateTime CreatedAtUtc,
    Guid? CreatedByUserId
);

// Draft save request
public class SaveDraftRequest
{
    [MaxLength(300)]
    public string? XText { get; set; }
    
    [MaxLength(500)]
    public string? WebTitle { get; set; }
    
    public string? WebBody { get; set; }
    
    [MaxLength(500)]
    public string? MobileSummary { get; set; }
    
    [MaxLength(100)]
    public string? PushTitle { get; set; }
    
    [MaxLength(300)]
    public string? PushBody { get; set; }
    
    [MaxLength(500)]
    public string? HashtagsCsv { get; set; }
    
    [MaxLength(500)]
    public string? MentionsCsv { get; set; }
    
    [MaxLength(2000)]
    public string? EditorialNote { get; set; }
    
    // Channel toggles (default true if not specified)
    public bool? PublishToWeb { get; set; }
    public bool? PublishToMobile { get; set; }
    public bool? PublishToX { get; set; }
    public bool? PublishToInstagram { get; set; }
    
    // Instagram caption override
    [MaxLength(2200)]
    public string? InstagramCaptionOverride { get; set; }
}

public record SaveDraftResponse(
    EditorialDraftDto Draft,
    int LatestVersionNo
);

// Reject request
public class RejectRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

// Schedule request
public class ScheduleRequest
{
    [Required]
    public DateTime ScheduledAtUtc { get; set; }
}

// Query params for inbox
public class EditorialInboxQuery
{
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public Guid? SourceId { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// Revision snapshot structure (for JSON serialization)
public class RevisionSnapshot
{
    public string Status { get; set; } = string.Empty;
    public string? DecisionType { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public string? EditorialNote { get; set; }
    public string? RejectionReason { get; set; }
    public DraftSnapshot? Draft { get; set; }
}

public class DraftSnapshot
{
    public string? XText { get; set; }
    public string? WebTitle { get; set; }
    public string? WebBody { get; set; }
    public string? MobileSummary { get; set; }
    public string? PushTitle { get; set; }
    public string? PushBody { get; set; }
    public string? HashtagsCsv { get; set; }
    public string? MentionsCsv { get; set; }
    public bool PublishToWeb { get; set; }
    public bool PublishToMobile { get; set; }
    public bool PublishToX { get; set; }
    public bool PublishToInstagram { get; set; }
    public string? InstagramCaptionOverride { get; set; }
}

// Correction request (Sprint 7)
public class CorrectionRequest : SaveDraftRequest
{
    [MaxLength(2000)]
    public string? CorrectionNote { get; set; }
}

// Correction result
public class CorrectionResult
{
    public bool Success { get; set; }
    public int VersionNo { get; set; }
    public Guid? JobId { get; set; }
    public string? Error { get; set; }
}

// Correction response DTO
public record CorrectionResponse(
    bool Ok,
    int VersionNo,
    Guid? JobId,
    string? Error
);

