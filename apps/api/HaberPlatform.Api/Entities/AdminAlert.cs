namespace HaberPlatform.Api.Entities;

/// <summary>
/// Admin alerts for system events (Sprint 8)
/// </summary>
public class AdminAlert
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Type: IngestionDown, FailoverActivated, ComplianceViolation, Retract
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Severity: Info, Warn, Critical
    /// </summary>
    public string Severity { get; set; } = AlertSeverities.Info;
    
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    
    public bool IsAcknowledged { get; set; } = false;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public User? AcknowledgedByUser { get; set; }
    
    /// <summary>
    /// JSON metadata (contentId, sourceId, etc.)
    /// </summary>
    public string? MetaJson { get; set; }
}

public static class AlertTypes
{
    public const string IngestionDown = "IngestionDown";
    public const string FailoverActivated = "FailoverActivated";
    public const string ComplianceViolation = "ComplianceViolation";
    public const string Retract = "Retract";
}

public static class AlertSeverities
{
    public const string Info = "Info";
    public const string Warn = "Warn";
    public const string Critical = "Critical";
}

