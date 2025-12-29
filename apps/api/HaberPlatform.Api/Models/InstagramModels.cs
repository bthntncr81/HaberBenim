using System.Text.Json.Serialization;

namespace HaberPlatform.Api.Models;

#region OAuth Flow DTOs

/// <summary>
/// Response from GET /connect - contains authorize URL
/// </summary>
public record InstagramConnectResponse(
    string AuthorizeUrl,
    string State
);

/// <summary>
/// Request to POST /exchange - exchange code for tokens
/// </summary>
public class InstagramExchangeRequest
{
    public required string Code { get; set; }
    public required string State { get; set; }
    public required string RedirectUri { get; set; }
}

/// <summary>
/// Response from POST /exchange - list of pages with Instagram accounts
/// </summary>
public record InstagramExchangeResponse(
    string FacebookUserId,
    List<InstagramPageInfo> Pages
);

/// <summary>
/// Page info returned from exchange
/// </summary>
public record InstagramPageInfo(
    string PageId,
    string PageName,
    string PageAccessToken,
    string? IgUserId,
    string? IgUsername,
    bool HasInstagram
);

/// <summary>
/// Request to POST /complete - finalize connection
/// </summary>
public class InstagramCompleteRequest
{
    public required string Name { get; set; }
    public required string PageId { get; set; }
    public required string State { get; set; }
}

/// <summary>
/// Response from POST /complete
/// </summary>
public record InstagramCompleteResponse(
    bool Success,
    string? Message,
    Guid? ConnectionId
);

/// <summary>
/// Connection status DTO
/// </summary>
public record InstagramConnectionDto(
    Guid Id,
    string Name,
    string PageId,
    string PageName,
    string IgUserId,
    string? IgUsername,
    string ScopesCsv,
    DateTime TokenExpiresAtUtc,
    bool IsDefaultPublisher,
    bool IsActive,
    DateTime CreatedAtUtc
);

/// <summary>
/// Response from GET /status
/// </summary>
public record InstagramConnectionListResponse(
    List<InstagramConnectionDto> Connections,
    int Count
);

#endregion

#region Meta Graph API Response Models

/// <summary>
/// Response from Facebook OAuth token exchange
/// </summary>
public class MetaTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
    
    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; set; }
    
    [JsonPropertyName("error")]
    public MetaErrorResponse? Error { get; set; }
}

/// <summary>
/// Error response from Meta Graph API
/// </summary>
public class MetaErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("error_subcode")]
    public int? ErrorSubcode { get; set; }
    
    [JsonPropertyName("fbtrace_id")]
    public string? FbTraceId { get; set; }
}

/// <summary>
/// Response from /me endpoint
/// </summary>
public class MetaMeResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("error")]
    public MetaErrorResponse? Error { get; set; }
}

/// <summary>
/// Response from /me/accounts (pages list)
/// </summary>
public class MetaAccountsResponse
{
    [JsonPropertyName("data")]
    public List<MetaPageData>? Data { get; set; }
    
    [JsonPropertyName("error")]
    public MetaErrorResponse? Error { get; set; }
}

/// <summary>
/// Page data from /me/accounts
/// </summary>
public class MetaPageData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

/// <summary>
/// Response from /{page_id}?fields=instagram_business_account
/// </summary>
public class MetaPageInstagramResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("instagram_business_account")]
    public MetaInstagramAccount? InstagramBusinessAccount { get; set; }
    
    [JsonPropertyName("error")]
    public MetaErrorResponse? Error { get; set; }
}

/// <summary>
/// Instagram business account info
/// </summary>
public class MetaInstagramAccount
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

/// <summary>
/// Response from creating media container
/// </summary>
public class MetaMediaContainerResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("error")]
    public MetaErrorResponse? Error { get; set; }
}

/// <summary>
/// Response from media_publish
/// </summary>
public class MetaMediaPublishResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("error")]
    public MetaErrorResponse? Error { get; set; }
}

#endregion

#region Publishing Models

/// <summary>
/// Result of Instagram publish operation
/// </summary>
public class InstagramPublishResult
{
    public bool Success { get; set; }
    public string? ContainerId { get; set; }
    public string? MediaId { get; set; }
    public string? Error { get; set; }
    public bool IsRateLimited { get; set; }
    public bool IsAuthError { get; set; }
    public int? RetryAfterSeconds { get; set; }
}

#endregion

#region Configuration

/// <summary>
/// Meta Graph API configuration options
/// </summary>
public class MetaGraphOptions
{
    public string GraphVersion { get; set; } = "v24.0";
    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    
    /// <summary>
    /// Full base URL with version
    /// </summary>
    public string ApiBaseUrl => $"{BaseUrl}/{GraphVersion}";
}

/// <summary>
/// Instagram publishing configuration
/// </summary>
public class InstagramPublishingOptions
{
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Max caption length (Instagram limit is 2200)
    /// </summary>
    public int MaxCaptionLength { get; set; } = 2200;
    
    /// <summary>
    /// Default hashtags to append (optional)
    /// </summary>
    public string? DefaultHashtags { get; set; }
}

#endregion

