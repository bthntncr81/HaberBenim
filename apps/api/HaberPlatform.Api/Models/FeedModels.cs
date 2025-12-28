namespace HaberPlatform.Api.Models;

public record FeedItemDto(
    Guid Id,
    DateTime PublishedAtUtc,
    Guid SourceId,
    string SourceName,
    string Title,
    string? Summary,
    string? CanonicalUrl,
    string Status,
    int DuplicateCount,
    // Decision fields
    string? DecisionType,
    Guid? DecidedByRuleId,
    string? DecisionReason,
    DateTime? DecidedAtUtc,
    DateTime? ScheduledAtUtc,
    int? TrustLevelSnapshot
);

public record FeedItemDetailDto(
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
    int DuplicateCount,
    // Decision fields
    string? DecisionType,
    Guid? DecidedByRuleId,
    string? DecisionReason,
    DateTime? DecidedAtUtc,
    DateTime? ScheduledAtUtc,
    int? TrustLevelSnapshot,
    List<MediaDto> Media
);

public record MediaDto(
    Guid Id,
    string MediaType,
    string Url,
    string? ThumbUrl
);

public record FeedResponse(
    List<FeedItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record IngestionRunResponse(
    bool Ok,
    int SourcesProcessed,
    int ItemsInserted,
    int Duplicates,
    List<string>? Errors,
    Dictionary<string, int>? ByDecisionTypeCounts
);

// Legacy source DTOs - use SourceModels.cs for new code
// Kept for backward compatibility with any existing code

[Obsolete("Use SourceListItemDto or SourceDetailDto from SourceModels.cs")]
public record SourceDto(
    Guid Id,
    string Name,
    string Type,
    string? Url,
    string? Description,
    string? Group,
    int TrustLevel,
    int Priority,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastFetchedAtUtc
);

[Obsolete("Use UpsertSourceRequest from SourceModels.cs")]
public record CreateSourceRequest(
    string Name,
    string Type,
    string? Url,
    string? Description,
    string? Group,
    int TrustLevel = 1,
    int Priority = 0
);

[Obsolete("Use UpsertSourceRequest from SourceModels.cs")]
public record UpdateSourceRequest(
    string? Name,
    string? Url,
    string? Description,
    string? Group,
    int? TrustLevel,
    int? Priority,
    bool? IsActive
);
