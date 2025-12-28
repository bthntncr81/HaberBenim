namespace HaberPlatform.Api.Entities;

/// <summary>
/// Versioned snapshot of content edits and status changes
/// </summary>
public class ContentRevision
{
    public Guid Id { get; set; }
    
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    
    public int VersionNo { get; set; }
    
    // JSON snapshot of draft and status at this point
    public string SnapshotJson { get; set; } = "{}";
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    
    // Action that triggered this revision
    public string ActionType { get; set; } = "DraftSaved";
}

public static class RevisionActionTypes
{
    public const string DraftSaved = "DraftSaved";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Scheduled = "Scheduled";
    public const string Corrected = "Corrected";
}

