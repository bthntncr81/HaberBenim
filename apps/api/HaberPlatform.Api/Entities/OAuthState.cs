namespace HaberPlatform.Api.Entities;

/// <summary>
/// Temporary storage for OAuth2 PKCE state during authorization flow
/// Expires after 10 minutes
/// </summary>
public class OAuthState
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Random state parameter for CSRF protection
    /// </summary>
    public required string State { get; set; }
    
    /// <summary>
    /// PKCE code_verifier (stored server-side, never sent to client)
    /// </summary>
    public required string CodeVerifier { get; set; }
    
    /// <summary>
    /// Which provider this is for (e.g., "X")
    /// </summary>
    public required string Provider { get; set; }
    
    /// <summary>
    /// When this state was created
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this state expires (default 10 minutes from creation)
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}

