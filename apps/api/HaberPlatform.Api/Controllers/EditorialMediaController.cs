using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Media;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// Editorial endpoints for managing content media
/// </summary>
[ApiController]
[Route("api/v1/editorial/items/{contentId:guid}/media")]
[Authorize(Roles = "Admin,Editor")]
public class EditorialMediaController : ControllerBase
{
    private readonly MediaPipelineService _mediaPipeline;
    private readonly IImageGenerator _imageGenerator;
    private readonly ILogger<EditorialMediaController> _logger;

    public EditorialMediaController(
        MediaPipelineService mediaPipeline,
        IImageGenerator imageGenerator,
        ILogger<EditorialMediaController> logger)
    {
        _mediaPipeline = mediaPipeline;
        _imageGenerator = imageGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Get all media for a content item
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<MediaAssetDto>>> GetMedia(Guid contentId, CancellationToken ct)
    {
        var media = await _mediaPipeline.GetMediaForContentAsync(contentId, ct);
        return Ok(media);
    }

    /// <summary>
    /// Refresh media from source (re-discover and download)
    /// </summary>
    [HttpPost("refresh-from-source")]
    public async Task<ActionResult<MediaRefreshResponse>> RefreshFromSource(Guid contentId, CancellationToken ct)
    {
        var asset = await _mediaPipeline.RefreshFromSourceAsync(contentId, ct);

        if (asset == null)
        {
            return Ok(new MediaRefreshResponse(
                Success: false,
                Message: "No media found from source",
                AssetId: null,
                PublicUrl: null
            ));
        }

        return Ok(new MediaRefreshResponse(
            Success: true,
            Message: "Media refreshed from source",
            AssetId: asset.Id,
            PublicUrl: _mediaPipeline.GetPublicUrl(asset.StoragePath)
        ));
    }

    /// <summary>
    /// Generate AI image for content using external API (Pollinations)
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ImageGenerationResponse>> GenerateImage(
        Guid contentId,
        [FromBody] GenerateImageRequest? request,
        CancellationToken ct)
    {
        try
        {
            // Check if AI generation is available
            var isAvailable = await _imageGenerator.IsAvailableAsync(ct);
            if (!isAvailable)
            {
                return StatusCode(503, new ImageGenerationResponse(
                    Success: false,
                    Message: "AI image generation is not available or disabled",
                    Error: "Generator not configured or disabled",
                    AssetId: null,
                    PublicUrl: null
                ));
            }

            // Check if force is false and primary already exists
            var force = request?.Force ?? false;
            if (!force)
            {
                var existingMedia = await _mediaPipeline.GetMediaForContentAsync(contentId, ct);
                var primaryExists = existingMedia.Any(m => m.IsPrimary);
                if (primaryExists)
                {
                    var primary = existingMedia.First(m => m.IsPrimary);
                    return Ok(new ImageGenerationResponse(
                        Success: true,
                        Message: "Primary image already exists. Use force=true to regenerate.",
                        Error: null,
                        AssetId: primary.Id,
                        PublicUrl: primary.PublicUrl,
                        PromptUsed: null,
                        Width: primary.Width,
                        Height: primary.Height
                    ));
                }
            }

            var result = await _mediaPipeline.GenerateImageAsync(
                contentId,
                request?.PromptOverride,
                null, // stylePreset not used anymore
                ct);

            if (!result.Success)
            {
                return Ok(new ImageGenerationResponse(
                    Success: false,
                    Message: "Image generation failed",
                    Error: result.Error,
                    AssetId: null,
                    PublicUrl: null
                ));
            }

            // Get the created asset
            var media = await _mediaPipeline.GetMediaForContentAsync(contentId, ct);
            var newAsset = result.AssetId.HasValue 
                ? media.FirstOrDefault(m => m.Id == result.AssetId.Value)
                : media.FirstOrDefault(m => m.IsPrimary);

            return Ok(new ImageGenerationResponse(
                Success: true,
                Message: "Image generated successfully using Pollinations AI",
                Error: null,
                AssetId: result.AssetId,
                PublicUrl: newAsset?.PublicUrl,
                PromptUsed: result.PromptUsed,
                Width: newAsset?.Width,
                Height: newAsset?.Height
            ));
        }
        catch (ExternalImageGeneratorException ex)
        {
            _logger.LogWarning(ex, "External image generator error for content {ContentId}: {StatusCode}", 
                contentId, ex.StatusCode);
            return StatusCode(ex.StatusCode, new ImageGenerationResponse(
                Success: false,
                Message: ex.StatusCode == 504 
                    ? "Image generation timed out. Please try again."
                    : "External image generator error",
                Error: ex.Message,
                AssetId: null,
                PublicUrl: null
            ));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during image generation for content {ContentId}", contentId);
            return StatusCode(503, new ImageGenerationResponse(
                Success: false,
                Message: "Could not connect to image generation service",
                Error: ex.Message,
                AssetId: null,
                PublicUrl: null
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image for content {ContentId}", contentId);
            return StatusCode(500, new ImageGenerationResponse(
                Success: false,
                Message: "An unexpected error occurred during image generation",
                Error: ex.Message,
                AssetId: null,
                PublicUrl: null
            ));
        }
    }

    /// <summary>
    /// Check AI image generator status
    /// </summary>
    [HttpGet("generator-status")]
    public async Task<ActionResult<GeneratorStatusResponse>> GetGeneratorStatus(CancellationToken ct)
    {
        var isAvailable = await _imageGenerator.IsAvailableAsync(ct);
        var provider = _imageGenerator is PollinationsImageGenerator ? "Pollinations" : "Unknown";
        
        return Ok(new GeneratorStatusResponse(
            IsAvailable: isAvailable,
            Message: isAvailable ? "AI image generator is available" : "AI image generator is disabled",
            Provider: provider
        ));
    }

    /// <summary>
    /// Set an asset as the primary image for content
    /// </summary>
    [HttpPost("set-primary/{assetId:guid}")]
    public async Task<ActionResult> SetPrimary(Guid contentId, Guid assetId, CancellationToken ct)
    {
        var success = await _mediaPipeline.SetPrimaryAsync(contentId, assetId, ct);

        if (!success)
        {
            return NotFound(new { error = "Asset not linked to this content" });
        }

        return Ok(new { message = "Primary image updated" });
    }

    /// <summary>
    /// Remove a media link from content
    /// </summary>
    [HttpDelete("{assetId:guid}")]
    public async Task<ActionResult> RemoveMedia(Guid contentId, Guid assetId, CancellationToken ct)
    {
        var success = await _mediaPipeline.RemoveMediaAsync(contentId, assetId, ct);

        if (!success)
        {
            return NotFound(new { error = "Asset not linked to this content" });
        }

        return Ok(new { message = "Media removed from content" });
    }

    /// <summary>
    /// Ensure content has a primary image (auto-discover or generate)
    /// </summary>
    [HttpPost("ensure-primary")]
    public async Task<ActionResult<MediaEnsureResponse>> EnsurePrimary(Guid contentId, CancellationToken ct)
    {
        try
        {
            var asset = await _mediaPipeline.EnsurePrimaryImageAsync(contentId, ct);

            if (asset == null)
            {
                return Ok(new MediaEnsureResponse(
                    Success: false,
                    Message: "Could not ensure primary image (no source media or AI disabled)",
                    AssetId: null,
                    Origin: null,
                    PublicUrl: null
                ));
            }

            return Ok(new MediaEnsureResponse(
                Success: true,
                Message: "Primary image ensured",
                AssetId: asset.Id,
                Origin: asset.Origin,
                PublicUrl: _mediaPipeline.GetPublicUrl(asset.StoragePath)
            ));
        }
        catch (ExternalImageGeneratorException ex)
        {
            _logger.LogWarning(ex, "External generator error while ensuring primary image for content {ContentId}", contentId);
            return StatusCode(ex.StatusCode, new MediaEnsureResponse(
                Success: false,
                Message: ex.StatusCode == 504 ? "Image generation timed out" : "External generator error",
                AssetId: null,
                Origin: null,
                PublicUrl: null
            ));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while ensuring primary image for content {ContentId}", contentId);
            return StatusCode(503, new MediaEnsureResponse(
                Success: false,
                Message: "Image generation service unavailable",
                AssetId: null,
                Origin: null,
                PublicUrl: null
            ));
        }
    }
}

public record MediaRefreshResponse(
    bool Success,
    string Message,
    Guid? AssetId,
    string? PublicUrl
);

public record MediaEnsureResponse(
    bool Success,
    string Message,
    Guid? AssetId,
    string? Origin,
    string? PublicUrl
);

public record ImageGenerationResponse(
    bool Success,
    string Message,
    string? Error,
    Guid? AssetId,
    string? PublicUrl,
    string? PromptUsed = null,
    int? Width = null,
    int? Height = null
);

public record GeneratorStatusResponse(
    bool IsAvailable,
    string Message,
    string Provider
);
