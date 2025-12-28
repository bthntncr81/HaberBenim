using System.Text.Json.Serialization;

namespace HaberPlatform.Api.Models;

// ================================
// OAuth2 PKCE Flow Models
// ================================

public record ConnectResponse(string AuthorizeUrl, string State);

public record ConnectionStatusDto(
    Guid Id,
    string Name,
    string XUsername,
    string XUserId,
    string Scopes,
    DateTime ExpiresAtUtc,
    bool IsDefaultPublisher,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record ConnectionListResponse(List<ConnectionStatusDto> Connections, int Count);

public record SetDefaultRequest(string? Name = null);

public record TestPostRequest(string Text);

// ================================
// X OAuth2 Token Response
// ================================

public class XTokenResponse
{
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    // Error fields
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

// ================================
// X API User Response
// ================================

public class XUserResponse
{
    [JsonPropertyName("data")]
    public XUserData? Data { get; set; }
}

public class XUserData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class XUserByUsernameResponse
{
    [JsonPropertyName("data")]
    public XUserData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<XApiError>? Errors { get; set; }
}

// ================================
// X API Tweets Response
// ================================

public class XTweetsResponse
{
    [JsonPropertyName("data")]
    public List<XTweetData>? Data { get; set; }

    [JsonPropertyName("meta")]
    public XTweetsMeta? Meta { get; set; }

    [JsonPropertyName("errors")]
    public List<XApiError>? Errors { get; set; }
}

public class XTweetData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("author_id")]
    public string? AuthorId { get; set; }

    [JsonPropertyName("conversation_id")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("attachments")]
    public XTweetAttachments? Attachments { get; set; }

    [JsonPropertyName("entities")]
    public XTweetEntities? Entities { get; set; }
}

public class XTweetAttachments
{
    [JsonPropertyName("media_keys")]
    public List<string>? MediaKeys { get; set; }
}

public class XTweetEntities
{
    [JsonPropertyName("urls")]
    public List<XUrlEntity>? Urls { get; set; }

    [JsonPropertyName("hashtags")]
    public List<XHashtagEntity>? Hashtags { get; set; }

    [JsonPropertyName("mentions")]
    public List<XMentionEntity>? Mentions { get; set; }
}

public class XUrlEntity
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("expanded_url")]
    public string? ExpandedUrl { get; set; }

    [JsonPropertyName("display_url")]
    public string? DisplayUrl { get; set; }
}

public class XHashtagEntity
{
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }
}

public class XMentionEntity
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class XTweetsMeta
{
    [JsonPropertyName("result_count")]
    public int ResultCount { get; set; }

    [JsonPropertyName("newest_id")]
    public string? NewestId { get; set; }

    [JsonPropertyName("oldest_id")]
    public string? OldestId { get; set; }

    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; }
}

// ================================
// X API Post Tweet Response
// ================================

public class XPostTweetRequest
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

public class XPostTweetResponse
{
    [JsonPropertyName("data")]
    public XPostedTweetData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<XApiError>? Errors { get; set; }
}

public class XPostedTweetData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// ================================
// X API Single Tweet Response
// ================================

public class XSingleTweetResponse
{
    [JsonPropertyName("data")]
    public XTweetData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<XApiError>? Errors { get; set; }
}

// ================================
// X API Delete Tweet Response
// ================================

public class XDeleteTweetResponse
{
    [JsonPropertyName("data")]
    public XDeleteTweetData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<XApiError>? Errors { get; set; }
}

public class XDeleteTweetData
{
    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}

// ================================
// X API Error Response
// ================================

public class XApiError
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public class XApiErrorResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("errors")]
    public List<XApiError>? Errors { get; set; }
}

// ================================
// X Ingestion Settings
// ================================

public class XIngestionOptions
{
    public const string SectionName = "XIngestion";

    /// <summary>
    /// Polling interval in seconds (default 60)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Max tweets to fetch per request (10-100)
    /// </summary>
    public int MaxResultsPerRequest { get; set; } = 50;

    /// <summary>
    /// Number of consecutive failures before creating an alert
    /// </summary>
    public int AlertAfterFailures { get; set; } = 3;
}

