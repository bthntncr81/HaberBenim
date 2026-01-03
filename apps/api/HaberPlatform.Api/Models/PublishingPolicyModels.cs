using System.Text.Json.Serialization;

namespace HaberPlatform.Api.Models;

/// <summary>
/// Publishing policy configuration (stored as SystemSetting JSON)
/// </summary>
public class PublishingPolicy
{
    /// <summary>
    /// Per-platform settings
    /// </summary>
    public Dictionary<string, PlatformPolicy> Platforms { get; set; } = new()
    {
        ["Instagram"] = new PlatformPolicy(),
        ["X"] = new PlatformPolicy(),
        ["TikTok"] = new PlatformPolicy(),
        ["YouTube"] = new PlatformPolicy(),
        ["Web"] = new PlatformPolicy { DailyLimit = 0 }, // No limit for web
        ["Mobile"] = new PlatformPolicy { DailyLimit = 0 }
    };

    /// <summary>
    /// Emergency detection rules
    /// </summary>
    public EmergencyRules EmergencyRules { get; set; } = new();

    /// <summary>
    /// Default timezone for scheduling (e.g., "Europe/Istanbul")
    /// </summary>
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
}

/// <summary>
/// Platform-specific publishing policy
/// </summary>
public class PlatformPolicy
{
    /// <summary>
    /// Allowed publishing time windows (empty = 24/7)
    /// </summary>
    public List<TimeWindow> AllowedWindows { get; set; } = new()
    {
        new TimeWindow { Start = "08:00", End = "23:00" }
    };

    /// <summary>
    /// Maximum posts per day (0 = unlimited)
    /// </summary>
    public int DailyLimit { get; set; } = 10;

    /// <summary>
    /// Minimum minutes between posts (rate limiting)
    /// </summary>
    public int MinIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Night mode settings
    /// </summary>
    public NightModeSettings NightMode { get; set; } = new();

    /// <summary>
    /// Allow emergency content to override schedule
    /// </summary>
    public bool EmergencyOverride { get; set; } = true;

    /// <summary>
    /// Platform is enabled for publishing
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Time window definition
/// </summary>
public class TimeWindow
{
    /// <summary>
    /// Start time in HH:mm format
    /// </summary>
    public string Start { get; set; } = "08:00";

    /// <summary>
    /// End time in HH:mm format
    /// </summary>
    public string End { get; set; } = "23:00";

    public TimeSpan StartTime => TimeSpan.Parse(Start);
    public TimeSpan EndTime => TimeSpan.Parse(End);

    /// <summary>
    /// Check if a time is within this window
    /// </summary>
    public bool Contains(TimeSpan time)
    {
        if (StartTime <= EndTime)
        {
            // Normal window (e.g., 08:00 - 23:00)
            return time >= StartTime && time < EndTime;
        }
        else
        {
            // Overnight window (e.g., 22:00 - 06:00)
            return time >= StartTime || time < EndTime;
        }
    }
}

/// <summary>
/// Night mode settings (silence notifications)
/// </summary>
public class NightModeSettings
{
    /// <summary>
    /// Night mode start time (HH:mm)
    /// </summary>
    public string Start { get; set; } = "23:00";

    /// <summary>
    /// Night mode end time (HH:mm)
    /// </summary>
    public string End { get; set; } = "08:00";

    /// <summary>
    /// Silence push notifications during night mode
    /// </summary>
    public bool SilencePush { get; set; } = true;

    /// <summary>
    /// Queue non-emergency content for morning
    /// </summary>
    public bool QueueForMorning { get; set; } = false;

    public TimeSpan StartTime => TimeSpan.Parse(Start);
    public TimeSpan EndTime => TimeSpan.Parse(End);

    /// <summary>
    /// Check if a time is within night mode
    /// </summary>
    public bool IsNightTime(TimeSpan time)
    {
        if (StartTime <= EndTime)
        {
            return time >= StartTime && time < EndTime;
        }
        else
        {
            // Overnight (e.g., 23:00 - 08:00)
            return time >= StartTime || time < EndTime;
        }
    }
}

/// <summary>
/// Emergency content detection rules
/// </summary>
public class EmergencyRules
{
    /// <summary>
    /// Keywords that trigger emergency priority (case-insensitive)
    /// </summary>
    public List<string> Keywords { get; set; } = new()
    {
        "son dakika",
        "acil",
        "flaş",
        "breaking",
        "deprem",
        "saldırı",
        "patlama",
        "sel",
        "yangın",
        "ölüm",
        "kaza"
    };

    /// <summary>
    /// Categories that are always treated as potential emergency
    /// </summary>
    public List<string> EmergencyCategories { get; set; } = new()
    {
        "Gündem",
        "Son Dakika",
        "Afet"
    };

    /// <summary>
    /// Source types that can trigger emergency (e.g., verified news)
    /// </summary>
    public List<string> TrustedSources { get; set; } = new();

    /// <summary>
    /// Minimum keyword match score to trigger emergency (1-10)
    /// </summary>
    public int MinKeywordScore { get; set; } = 1;
}

/// <summary>
/// Scheduling result
/// </summary>
public class ScheduleResult
{
    public bool CanPublishNow { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string? Reason { get; set; }
    public bool IsEmergency { get; set; }
    public bool SilencePush { get; set; }
    public int DailyCountSoFar { get; set; }
    public int DailyLimit { get; set; }

    public static ScheduleResult Now(bool isEmergency = false, bool silencePush = false) => new()
    {
        CanPublishNow = true,
        ScheduledAtUtc = DateTime.UtcNow,
        IsEmergency = isEmergency,
        SilencePush = silencePush
    };

    public static ScheduleResult Scheduled(DateTime scheduledAtUtc, string reason, int dailyCount = 0, int limit = 0) => new()
    {
        CanPublishNow = false,
        ScheduledAtUtc = scheduledAtUtc,
        Reason = reason,
        DailyCountSoFar = dailyCount,
        DailyLimit = limit
    };
}

/// <summary>
/// Emergency queue item
/// </summary>
public class EmergencyQueueItemDto
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    public string Title { get; set; } = "";
    public string? Category { get; set; }
    public string SourceName { get; set; } = "";
    public int Priority { get; set; }
    public string Status { get; set; } = "";
    public List<string> MatchedKeywords { get; set; } = new();
    public DateTime DetectedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}

/// <summary>
/// Daily publishing stats
/// </summary>
public class DailyPublishingStats
{
    public string Platform { get; set; } = "";
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public int Limit { get; set; }
    public int Remaining => Math.Max(0, Limit - Count);
    public bool IsAtLimit => Limit > 0 && Count >= Limit;
}

