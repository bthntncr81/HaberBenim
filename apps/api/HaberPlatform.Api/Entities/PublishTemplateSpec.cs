namespace HaberPlatform.Api.Entities;

public class PublishTemplateSpec
{
    public Guid Id { get; set; }
    
    public Guid TemplateId { get; set; }
    public PublishTemplate Template { get; set; } = null!;
    
    /// <summary>Designer visual output JSON (canvas, layers)</summary>
    public string? VisualSpecJson { get; set; }
    
    /// <summary>Text/caption templates per platform</summary>
    public string? TextSpecJson { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

