namespace HaberPlatform.Api.Entities;

/// <summary>
/// Tracks ingestion health status for each source (Sprint 8)
/// </summary>
public class SourceIngestionHealth
{
    public Guid Id { get; set; }
    
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    
    public DateTime LastSuccessAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; } = 0;
    
    /// <summary>
    /// Status: Healthy, Degraded, Down
    /// </summary>
    public string Status { get; set; } = HealthStatuses.Healthy;
}

public static class HealthStatuses
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Down = "Down";
}

