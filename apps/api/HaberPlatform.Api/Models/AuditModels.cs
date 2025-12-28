namespace HaberPlatform.Api.Models;

/// <summary>
/// Audit log DTO
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// Audit log list response
/// </summary>
public class AuditLogListResponse
{
    public List<AuditLogDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Audit log query params
/// </summary>
public class AuditLogQueryParams
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? UserEmail { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

