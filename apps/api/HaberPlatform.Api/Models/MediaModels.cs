namespace HaberPlatform.Api.Models;

/// <summary>
/// Media storage configuration options
/// </summary>
public class MediaOptions
{
    /// <summary>Root directory for storing media files</summary>
    public string RootDir { get; set; } = "tools/storage/media";
    
    /// <summary>Public URL base path for serving media</summary>
    public string PublicBasePath { get; set; } = "/media";
    
    /// <summary>Maximum allowed file size in bytes (default 5MB)</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    
    /// <summary>Allowed MIME types for upload/download</summary>
    public string[] AllowedContentTypes { get; set; } = 
        ["image/jpeg", "image/png", "image/webp", "image/gif"];
}

/// <summary>
/// AI image generation configuration
/// </summary>
public class AIImageOptions
{
    /// <summary>Whether AI image generation is enabled</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>Provider: "PollinationsLegacy"</summary>
    public string Provider { get; set; } = "PollinationsLegacy";
    
    /// <summary>Pollinations API settings</summary>
    public PollinationsSettings Pollinations { get; set; } = new();
    
    /// <summary>Target width for generated images</summary>
    public int TargetWidth { get; set; } = 1280;
    
    /// <summary>Target height for generated images</summary>
    public int TargetHeight { get; set; } = 720;
    
    /// <summary>Prompt template with placeholders: {title}, {category}, {sourceName}, {summary}</summary>
    public string PromptTemplate { get; set; } = 
        "News thumbnail illustration, 16:9, modern flat vector illustration, abstract, no real persons, no faces, no logos, no trademarks, no text. Topic: {title}. Category: {category}. Summary: {summary}. Source: {sourceName}.";
    
    /// <summary>Safety clauses always appended to prompt</summary>
    public string SafetyClauses { get; set; } = 
        "flat illustration, abstract, no real persons, no faces, no logos, no trademarks, no text overlay";
}

/// <summary>
/// Pollinations.ai API configuration
/// </summary>
public class PollinationsSettings
{
    /// <summary>Base URL - prompt is appended and URL-encoded</summary>
    public string BaseUrl { get; set; } = "https://image.pollinations.ai/prompt/";
    
    /// <summary>HTTP request timeout in seconds (Pollinations can be slow)</summary>
    public int TimeoutSeconds { get; set; } = 180;
    
    /// <summary>User-Agent header</summary>
    public string UserAgent { get; set; } = "HaberPlatform/1.0";
    
    /// <summary>Max retries on timeout/error</summary>
    public int MaxRetries { get; set; } = 2;
}

#region DTOs

/// <summary>
/// Media asset response DTO
/// </summary>
public record MediaAssetDto(
    Guid Id,
    string Kind,
    string Origin,
    string? SourceUrl,
    string StoragePath,
    string ContentType,
    long SizeBytes,
    int Width,
    int Height,
    string? AltText,
    bool IsPrimary,
    int SortOrder,
    string PublicUrl,
    DateTime CreatedAtUtc
);

/// <summary>
/// Request to generate AI image
/// </summary>
public class GenerateImageRequest
{
    /// <summary>Force regeneration even if primary image exists</summary>
    public bool Force { get; set; } = false;
    
    /// <summary>Optional override for AI prompt (safety clauses still added)</summary>
    public string? PromptOverride { get; set; }
}

/// <summary>
/// Discovered media candidate from source
/// </summary>
public record MediaCandidate(
    string Url,
    string? AltText,
    string Origin,
    string? ContentType = null
);

/// <summary>
/// Result of media download operation
/// </summary>
public class MediaDownloadResult
{
    public bool Success { get; set; }
    public Guid? AssetId { get; set; }
    public string? Error { get; set; }
    public string? StoragePath { get; set; }
    
    public static MediaDownloadResult Succeeded(Guid assetId, string storagePath) =>
        new() { Success = true, AssetId = assetId, StoragePath = storagePath };
    
    public static MediaDownloadResult Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Result of AI image generation
/// </summary>
public class ImageGenerationResult
{
    public bool Success { get; set; }
    public Guid? AssetId { get; set; }
    public string? Error { get; set; }
    public string? PromptUsed { get; set; }
    
    public static ImageGenerationResult Succeeded(Guid assetId, string prompt) =>
        new() { Success = true, AssetId = assetId, PromptUsed = prompt };
    
    public static ImageGenerationResult Failed(string error) =>
        new() { Success = false, Error = error };
}

#endregion
