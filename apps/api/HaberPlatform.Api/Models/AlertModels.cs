namespace HaberPlatform.Api.Models;

/// <summary>
/// Admin alert DTO
/// </summary>
public class AdminAlertDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedByUserEmail { get; set; }
    public string? MetaJson { get; set; }
}

/// <summary>
/// Alert list response
/// </summary>
public class AlertListResponse
{
    public List<AdminAlertDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Alert query params
/// </summary>
public class AlertQueryParams
{
    public string? Severity { get; set; }
    public string? Type { get; set; }
    public bool? Acknowledged { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Alert acknowledge response
/// </summary>
public class AlertAckResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}

