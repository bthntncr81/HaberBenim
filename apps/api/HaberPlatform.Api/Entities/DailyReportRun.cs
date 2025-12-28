namespace HaberPlatform.Api.Entities;

/// <summary>
/// Record of daily report generation runs
/// </summary>
public class DailyReportRun
{
    public Guid Id { get; set; }
    
    // Report date in local timezone (e.g., Europe/Istanbul)
    public DateOnly ReportDateLocal { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    
    // Path to the generated XLSX file
    public string FilePath { get; set; } = string.Empty;
    
    // Status: Succeeded, Failed
    public string Status { get; set; } = DailyReportStatuses.Succeeded;
    
    public string? Error { get; set; }
}

public static class DailyReportStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

