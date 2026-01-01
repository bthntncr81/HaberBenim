namespace HaberPlatform.Api.Models;

/// <summary>
/// OpenAI Video generation configuration
/// </summary>
public class OpenAiVideoOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string Model { get; set; } = "sora-2";
    public string Seconds { get; set; } = "8";
    public string Size { get; set; } = "1280x720";
    public int TimeoutSeconds { get; set; } = 300;
    public bool Enabled { get; set; } = true;
    
    // Validation
    public static readonly string[] AllowedModels = ["sora-2", "sora-2-pro"];
    public static readonly string[] AllowedSeconds = ["4", "8", "12"];
    public static readonly string[] AllowedSizes = ["720x1280", "1280x720", "1024x1792", "1792x1024"];
}

/// <summary>
/// Request to generate AI video
/// </summary>
public record AiVideoGenerateRequest(
    bool Force = false,
    string Mode = "AutoPrompt", // AutoPrompt | CustomPrompt
    string? PromptOverride = null,
    string Model = "sora-2",
    string Seconds = "8",
    string Size = "1280x720"
);

/// <summary>
/// Response from video generation
/// </summary>
public record AiVideoJobDto(
    Guid Id,
    Guid ContentItemId,
    string Provider,
    string Model,
    string Prompt,
    string Seconds,
    string Size,
    string Status,
    string? OpenAiVideoId,
    int Progress,
    string? Error,
    string? MediaUrl,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc
);

/// <summary>
/// OpenAI video creation response
/// </summary>
public class OpenAiVideoCreateResponse
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public int Progress { get; set; }
    public OpenAiVideoError? Error { get; set; }
}

/// <summary>
/// OpenAI video status response
/// </summary>
public class OpenAiVideoStatusResponse
{
    public string? Id { get; set; }
    public string? Status { get; set; } // queued, in_progress, completed, failed
    public int Progress { get; set; }
    public string? FailedReason { get; set; }
    public OpenAiVideoError? Error { get; set; }
}

/// <summary>
/// OpenAI error response
/// </summary>
public class OpenAiVideoError
{
    public string? Message { get; set; }
    public string? Type { get; set; }
    public string? Code { get; set; }
}

/// <summary>
/// Prompt preview response
/// </summary>
public record AiVideoPromptPreviewResponse(
    string Prompt,
    string Model,
    string Seconds,
    string Size
);

