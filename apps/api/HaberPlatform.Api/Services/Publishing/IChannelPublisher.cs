using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Publishing;

public interface IChannelPublisher
{
    string ChannelName { get; }
    Task<PublishResult> PublishAsync(ContentItem item, ContentDraft draft, CancellationToken ct = default);
}

public class PublishResult
{
    public bool Success { get; set; }
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? Error { get; set; }
    
    /// <summary>
    /// External ID from the platform (e.g., tweet ID)
    /// </summary>
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// If true, the publish can be retried later
    /// </summary>
    public bool IsRetryable { get; set; }
    
    /// <summary>
    /// When to retry if IsRetryable is true
    /// </summary>
    public DateTime? RetryAfter { get; set; }

    public static PublishResult Succeeded(
        string? requestJson = null, 
        string? responseJson = null,
        string? externalId = null) => new()
    {
        Success = true,
        RequestJson = requestJson,
        ResponseJson = responseJson,
        ExternalId = externalId
    };

    public static PublishResult Failed(
        string error, 
        string? requestJson = null,
        string? responseJson = null,
        bool isRetryable = false,
        DateTime? retryAfter = null) => new()
    {
        Success = false,
        Error = error,
        RequestJson = requestJson,
        ResponseJson = responseJson,
        IsRetryable = isRetryable,
        RetryAfter = retryAfter
    };

    public static PublishResult Skipped(
        string reason, 
        string? requestJson = null,
        string? responseJson = null) => new()
    {
        Success = true, // Skipped is not a failure
        IsSkipped = true,
        SkipReason = reason,
        RequestJson = requestJson,
        ResponseJson = responseJson
    };
}
