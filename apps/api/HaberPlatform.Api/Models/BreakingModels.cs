namespace HaberPlatform.Api.Models;

/// <summary>
/// Request to mark content as breaking news
/// </summary>
public class MarkBreakingRequest
{
    public string? Note { get; set; }
    public int? Priority { get; set; }
    public bool? PushRequired { get; set; }
}

/// <summary>
/// Response for marking breaking news
/// </summary>
public class MarkBreakingResponse
{
    public bool Ok { get; set; }
    public int VersionNo { get; set; }
    public Guid? JobId { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Breaking news inbox item DTO
/// </summary>
public class BreakingInboxItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime BreakingAtUtc { get; set; }
    public string? BreakingNote { get; set; }
    public int BreakingPriority { get; set; }
    public bool BreakingPushRequired { get; set; }
    public Guid? BreakingByUserId { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public bool HasDraft { get; set; }
}

/// <summary>
/// Breaking inbox response
/// </summary>
public class BreakingInboxResponse
{
    public List<BreakingInboxItemDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Breaking inbox query params
/// </summary>
public class BreakingInboxParams
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

