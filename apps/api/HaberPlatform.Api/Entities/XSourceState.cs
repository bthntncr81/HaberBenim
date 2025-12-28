namespace HaberPlatform.Api.Entities;

/// <summary>
/// Tracks X/Twitter ingestion state per source
/// Used for polling tweets and tracking since_id
/// </summary>
public class XSourceState
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Foreign key to Source (where Type = "X")
    /// </summary>
    public Guid SourceId { get; set; }
    public Source? Source { get; set; }
    
    /// <summary>
    /// X/Twitter user ID to poll for tweets
    /// </summary>
    public string? XUserId { get; set; }
    
    /// <summary>
    /// Last tweet ID fetched (for pagination with since_id)
    /// </summary>
    public string? LastSinceId { get; set; }
    
    /// <summary>
    /// When we last attempted to poll this source
    /// </summary>
    public DateTime? LastPolledAtUtc { get; set; }
    
    /// <summary>
    /// When we last successfully fetched tweets
    /// </summary>
    public DateTime? LastSuccessAtUtc { get; set; }
    
    /// <summary>
    /// When we last encountered an error
    /// </summary>
    public DateTime? LastFailureAtUtc { get; set; }
    
    /// <summary>
    /// Last error message if any
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Count of consecutive failures (for alerting)
    /// </summary>
    public int ConsecutiveFailures { get; set; } = 0;
}

