namespace HaberPlatform.Api.Entities;

public class Source
{
    public Guid Id { get; set; }
    
    /// <summary>Display name e.g. "AA RSS", "TRT Haber", "@someaccount"</summary>
    public required string Name { get; set; }
    
    /// <summary>Type: "RSS", "X", "Manual", "GoogleNews"</summary>
    public required string Type { get; set; }
    
    /// <summary>For X: username without @; for RSS/other can be empty</summary>
    public string? Identifier { get; set; }
    
    /// <summary>Feed URL for RSS, website URL for others</summary>
    public string? Url { get; set; }
    
    /// <summary>Description of the source</summary>
    public string? Description { get; set; }
    
    /// <summary>Category: "Gundem", "Spor", "Ekonomi" etc.</summary>
    public string Category { get; set; } = "Gundem";
    
    /// <summary>Legacy group field, now use Category instead</summary>
    public string? Group { get; set; }
    
    /// <summary>Trust level 0-100 (higher = more trusted)</summary>
    public int TrustLevel { get; set; } = 50;
    
    /// <summary>Priority 0-1000 (higher = more important)</summary>
    public int Priority { get; set; } = 100;
    
    /// <summary>Whether the source is active for ingestion</summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>Default behavior: "Auto" or "Editorial"</summary>
    public string DefaultBehavior { get; set; } = "Editorial";
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastFetchedAtUtc { get; set; }
    public int FetchIntervalMinutes { get; set; } = 15;

    // Navigation properties
    public ICollection<ContentItem> ContentItems { get; set; } = new List<ContentItem>();
    
    // Ingestion health tracking (Sprint 8)
    public SourceIngestionHealth? IngestionHealth { get; set; }
    
    // X source state (Sprint 9)
    public XSourceState? XSourceState { get; set; }
}

/// <summary>Valid source types</summary>
public static class SourceTypes
{
    public const string RSS = "RSS";
    public const string X = "X";
    public const string Manual = "Manual";
    public const string GoogleNews = "GoogleNews";
    
    public static readonly string[] All = { RSS, X, Manual, GoogleNews };
    
    public static bool IsValid(string? type) => 
        !string.IsNullOrEmpty(type) && All.Contains(type);
}

/// <summary>Valid default behaviors</summary>
public static class DefaultBehaviors
{
    public const string Auto = "Auto";
    public const string Editorial = "Editorial";
    
    public static readonly string[] All = { Auto, Editorial };
    
    public static bool IsValid(string? behavior) => 
        !string.IsNullOrEmpty(behavior) && All.Contains(behavior);
}
