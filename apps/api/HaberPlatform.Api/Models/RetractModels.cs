namespace HaberPlatform.Api.Models;

/// <summary>
/// Request to retract (takedown) published content
/// </summary>
public class RetractRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Response for retract operation
/// </summary>
public class RetractResponse
{
    public bool Ok { get; set; }
    public int VersionNo { get; set; }
    public string? Error { get; set; }
}

