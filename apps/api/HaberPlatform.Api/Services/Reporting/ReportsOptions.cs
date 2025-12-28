namespace HaberPlatform.Api.Services.Reporting;

public class ReportsOptions
{
    public const string SectionName = "Reports";
    
    public string OutputDir { get; set; } = "tools/reports";
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
}

