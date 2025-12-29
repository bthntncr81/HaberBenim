namespace HaberPlatform.Api.Entities;

/// <summary>
/// Instagram Business/Creator account connection via Meta Graph API
/// Only Professional Instagram accounts connected to a Facebook Page are supported
/// </summary>
public class InstagramConnection
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Display name for this connection
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Facebook User ID who authorized
    /// </summary>
    public required string FacebookUserId { get; set; }
    
    /// <summary>
    /// Facebook Page ID connected to Instagram
    /// </summary>
    public required string PageId { get; set; }
    
    /// <summary>
    /// Facebook Page name
    /// </summary>
    public required string PageName { get; set; }
    
    /// <summary>
    /// Instagram Business Account ID (IG User) - required for publishing
    /// </summary>
    public required string IgUserId { get; set; }
    
    /// <summary>
    /// Instagram username (optional, for display)
    /// </summary>
    public string? IgUsername { get; set; }
    
    /// <summary>
    /// OAuth scopes granted (comma-separated)
    /// </summary>
    public required string ScopesCsv { get; set; }
    
    /// <summary>
    /// Page Access Token (encrypted) - long-lived, used for API calls
    /// </summary>
    public required string PageAccessTokenEncrypted { get; set; }
    
    /// <summary>
    /// When the token expires (long-lived tokens last ~60 days)
    /// </summary>
    public DateTime TokenExpiresAtUtc { get; set; }
    
    /// <summary>
    /// Whether this is the default connection for publishing
    /// </summary>
    public bool IsDefaultPublisher { get; set; } = false;
    
    /// <summary>
    /// Whether this connection is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

