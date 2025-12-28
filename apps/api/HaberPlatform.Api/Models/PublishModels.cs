namespace HaberPlatform.Api.Models;

// Publish job list DTO
public record PublishJobDto(
    Guid Id,
    Guid ContentItemId,
    string? ContentTitle,
    DateTime ScheduledAtUtc,
    int VersionNo,
    string Status,
    int AttemptCount,
    DateTime? LastAttemptAtUtc,
    DateTime? NextRetryAtUtc,
    string? LastError,
    DateTime CreatedAtUtc
);

public record PublishJobListResponse(
    List<PublishJobDto> Items,
    int Total,
    int Page,
    int PageSize
);

// Channel publish log DTO
public record ChannelPublishLogDto(
    Guid Id,
    string Channel,
    int VersionNo,
    string Status,
    DateTime CreatedAtUtc,
    string? RequestJson,
    string? ResponseJson,
    string? Error
);

// Enqueue response
public record EnqueueResponse(
    bool Success,
    bool AlreadyQueued,
    Guid? JobId,
    int VersionNo,
    DateTime? ScheduledAtUtc,
    string? Message,
    string? Error
);

// Run due response
public record RunDueResponse(
    int JobsProcessed,
    string Message
);

// Published content DTO (for public API)
public record PublishedContentDto(
    Guid Id,
    string WebTitle,
    string WebBody,
    string? CanonicalUrl,
    string? SourceName,
    string? CategoryOrGroup,
    DateTime PublishedAtUtc
);

public record PublishedContentListResponse(
    List<PublishedContentDto> Items,
    int Total,
    int Page,
    int PageSize
);

// Query params for jobs
public class PublishJobQueryParams
{
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

