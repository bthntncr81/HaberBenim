using System.ComponentModel.DataAnnotations;

namespace HaberPlatform.Api.Models;

/// <summary>
/// Request to save OpenAI API key
/// </summary>
public class SaveOpenAiKeyRequest
{
    [Required(ErrorMessage = "API key is required")]
    [MinLength(20, ErrorMessage = "API key must be at least 20 characters")]
    public required string ApiKey { get; set; }
    
    /// <summary>
    /// Optional Organization ID for OpenAI
    /// </summary>
    public string? OrgId { get; set; }
    
    /// <summary>
    /// Optional Project ID for OpenAI project scoping
    /// </summary>
    public string? ProjectId { get; set; }
}

/// <summary>
/// Response with OpenAI API key status
/// </summary>
public record OpenAiKeyStatusResponse(
    bool IsConfigured,
    string? KeyLast4,
    string? OrgId,
    string? ProjectId,
    DateTime? LastTestAtUtc,
    bool? LastTestOk,
    string? LastError,
    bool? Sora2Available
);

/// <summary>
/// Response from saving OpenAI key
/// </summary>
public record SaveOpenAiKeyResponse(
    bool Success,
    string? KeyLast4,
    string? Message
);

/// <summary>
/// Response from testing OpenAI connection
/// </summary>
public record TestOpenAiResponse(
    bool Success,
    bool? Sora2Available,
    int? ModelCount,
    string? Error
);

/// <summary>
/// OpenAI models list response (partial)
/// </summary>
public class OpenAiModelsResponse
{
    public List<OpenAiModel>? Data { get; set; }
}

public class OpenAiModel
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public string? OwnedBy { get; set; }
}
