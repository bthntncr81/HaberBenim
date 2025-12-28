namespace HaberPlatform.Api.Entities;

/// <summary>
/// Represents an OAuth2 connection to X (Twitter) API
/// Stores encrypted access/refresh tokens for user-context API calls
/// </summary>
public class XIntegrationConnection
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Friendly name for the connection (e.g., "Main Publisher", "News Account")
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// X/Twitter user ID from /2/users/me
    /// </summary>
    public required string XUserId { get; set; }
    
    /// <summary>
    /// X/Twitter username (handle without @)
    /// </summary>
    public required string XUsername { get; set; }
    
    /// <summary>
    /// OAuth2 scopes granted (comma-separated)
    /// </summary>
    public required string ScopesCsv { get; set; }
    
    /// <summary>
    /// Encrypted access token (using DataProtection)
    /// </summary>
    public required string AccessTokenEncrypted { get; set; }
    
    /// <summary>
    /// Encrypted refresh token (using DataProtection)
    /// </summary>
    public required string RefreshTokenEncrypted { get; set; }
    
    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    
    /// <summary>
    /// If true, this connection is used for publishing tweets
    /// </summary>
    public bool IsDefaultPublisher { get; set; }
    
    /// <summary>
    /// If true, connection is active and usable
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

