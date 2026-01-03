namespace HaberPlatform.Api.Entities;

public class TemplateAsset
{
    public Guid Id { get; set; }
    
    /// <summary>Unique key for referencing, e.g. "haberbenim_logo"</summary>
    public required string Key { get; set; }
    
    public required string ContentType { get; set; }
    
    /// <summary>Relative storage path e.g. "assets/haberbenim_logo.png"</summary>
    public required string StoragePath { get; set; }
    
    public int Width { get; set; }
    public int Height { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

