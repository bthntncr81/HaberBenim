namespace HaberPlatform.Api.Entities;

/// <summary>
/// Links a source to a publish template for a specific platform
/// </summary>
public class SourceTemplateAssignment
{
    public Guid Id { get; set; }
    
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    
    /// <summary>Instagram, X, TikTok, YouTube</summary>
    public required string Platform { get; set; }
    
    /// <summary>Auto (future: per-category buckets)</summary>
    public string Mode { get; set; } = "Auto";
    
    public Guid TemplateId { get; set; }
    public PublishTemplate Template { get; set; } = null!;
    
    /// <summary>Override template priority for this source (null = use template's priority)</summary>
    public int? PriorityOverride { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

