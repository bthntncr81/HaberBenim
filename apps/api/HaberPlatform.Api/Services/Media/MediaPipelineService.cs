using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Media;

/// <summary>
/// Orchestrates the media pipeline: discovery, download, AI generation
/// </summary>
public class MediaPipelineService
{
    private readonly AppDbContext _db;
    private readonly MediaDiscoveryService _discoveryService;
    private readonly MediaDownloadService _downloadService;
    private readonly IImageGenerator _imageGenerator;
    private readonly AIImageOptions _aiOptions;
    private readonly MediaOptions _mediaOptions;
    private readonly ILogger<MediaPipelineService> _logger;

    public MediaPipelineService(
        AppDbContext db,
        MediaDiscoveryService discoveryService,
        MediaDownloadService downloadService,
        IImageGenerator imageGenerator,
        IOptions<AIImageOptions> aiOptions,
        IOptions<MediaOptions> mediaOptions,
        ILogger<MediaPipelineService> logger)
    {
        _db = db;
        _discoveryService = discoveryService;
        _downloadService = downloadService;
        _imageGenerator = imageGenerator;
        _aiOptions = aiOptions.Value;
        _mediaOptions = mediaOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Ensure content has a primary image. Tries:
    /// 1. Use existing primary if available
    /// 2. Discover + download from source
    /// 3. Generate AI image if enabled and draft allows
    /// </summary>
    public async Task<MediaAsset?> EnsurePrimaryImageAsync(
        Guid contentItemId,
        CancellationToken ct = default)
    {
        // Load content with source and draft
        var item = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Include(c => c.Media) // Old RSS media
            .FirstOrDefaultAsync(c => c.Id == contentItemId, ct);

        if (item == null)
        {
            _logger.LogWarning("Content {ContentId} not found for media pipeline", contentItemId);
            return null;
        }

        // Check if primary already exists
        var existingPrimary = await _db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == contentItemId && l.IsPrimary)
            .Select(l => l.MediaAsset)
            .FirstOrDefaultAsync(ct);

        if (existingPrimary != null)
        {
            _logger.LogDebug("Content {ContentId} already has primary image {AssetId}", 
                contentItemId, existingPrimary.Id);
            return existingPrimary;
        }

        // Try to discover and download from source
        var discovered = await DiscoverAndDownloadAsync(item, ct);
        if (discovered != null)
        {
            return discovered;
        }

        // Try AI generation if enabled
        var draft = item.Draft;
        if (draft?.AutoGenerateImageIfMissing == true && _aiOptions.Enabled)
        {
            var generated = await GenerateAIImageAsync(item, draft, ct);
            if (generated != null)
            {
                return generated;
            }
        }

        _logger.LogDebug("No primary image available for content {ContentId}", contentItemId);
        return null;
    }

    /// <summary>
    /// Discover media from source and download first valid candidate
    /// </summary>
    public async Task<MediaAsset?> DiscoverAndDownloadAsync(
        ContentItem item,
        CancellationToken ct = default)
    {
        if (item.Source == null)
        {
            return null;
        }

        try
        {
            var candidates = await _discoveryService.DiscoverAsync(item, item.Source, item.OriginalText, ct);

            foreach (var candidate in candidates)
            {
                var result = await _downloadService.DownloadAndStoreAsync(candidate, item.Id, setPrimary: true, ct);
                if (result.Success && result.AssetId.HasValue)
                {
                    return await _db.MediaAssets.FindAsync([result.AssetId.Value], ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover/download media for content {ContentId}", item.Id);
        }

        return null;
    }

    /// <summary>
    /// Refresh media from source (re-discover and download)
    /// </summary>
    public async Task<MediaAsset?> RefreshFromSourceAsync(
        Guid contentItemId,
        CancellationToken ct = default)
    {
        var item = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Media)
            .FirstOrDefaultAsync(c => c.Id == contentItemId, ct);

        if (item == null)
        {
            return null;
        }

        return await DiscoverAndDownloadAsync(item, ct);
    }

    /// <summary>
    /// Generate AI image for content
    /// </summary>
    public async Task<ImageGenerationResult> GenerateImageAsync(
        Guid contentItemId,
        string? promptOverride = null,
        string? stylePreset = null,
        CancellationToken ct = default)
    {
        var item = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == contentItemId, ct);

        if (item == null)
        {
            return ImageGenerationResult.Failed("Content not found");
        }

        var draft = item.Draft;
        var asset = await GenerateAIImageAsync(item, draft, ct, promptOverride, stylePreset);

        if (asset != null)
        {
            return ImageGenerationResult.Succeeded(asset.Id, asset.GenerationPrompt ?? "");
        }

        return ImageGenerationResult.Failed("AI image generation failed or is disabled");
    }

    /// <summary>
    /// Generate AI image using external API (Pollinations)
    /// </summary>
    private async Task<MediaAsset?> GenerateAIImageAsync(
        ContentItem item,
        ContentDraft? draft,
        CancellationToken ct,
        string? promptOverride = null,
        string? stylePreset = null)
    {
        if (!_aiOptions.Enabled)
        {
            _logger.LogDebug("AI image generation is disabled");
            return null;
        }

        // Check if generator is available
        if (!await _imageGenerator.IsAvailableAsync(ct))
        {
            _logger.LogWarning("AI image generator is not available");
            return null;
        }

        // Build prompt with safety clauses
        var prompt = BuildPrompt(item, draft, promptOverride);

        _logger.LogInformation("Generating AI image for content {ContentId} with prompt: {Prompt}", 
            item.Id, prompt.Length > 100 ? prompt[..100] + "..." : prompt);

        // Generate using configured dimensions
        var width = _aiOptions.TargetWidth;
        var height = _aiOptions.TargetHeight;
        var output = await _imageGenerator.GenerateAsync(prompt, width, height, ct);

        if (output == null)
        {
            _logger.LogWarning("AI image generation returned no output for content {ContentId}", item.Id);
            return null;
        }

        // Store the generated image
        var result = await _downloadService.StoreFromBytesAsync(
            output.ImageBytes,
            output.ContentType,
            output.Width,
            output.Height,
            item.Id,
            MediaOrigins.AI,
            generationPrompt: prompt,
            setPrimary: true,
            ct);

        if (!result.Success || !result.AssetId.HasValue)
        {
            _logger.LogError("Failed to store AI-generated image: {Error}", result.Error);
            return null;
        }

        return await _db.MediaAssets.FindAsync([result.AssetId.Value], ct);
    }

    /// <summary>
    /// Build AI prompt from template and content - always includes safety clauses
    /// </summary>
    private string BuildPrompt(
        ContentItem item,
        ContentDraft? draft,
        string? promptOverride)
    {
        string basePrompt;
        
        if (!string.IsNullOrEmpty(promptOverride))
        {
            // Use override but still append safety clauses
            basePrompt = promptOverride;
        }
        else if (!string.IsNullOrEmpty(draft?.ImagePromptOverride))
        {
            // Use draft override but still append safety clauses
            basePrompt = draft.ImagePromptOverride;
        }
        else
        {
            // Build from template
            var template = _aiOptions.PromptTemplate;
            var title = draft?.WebTitle ?? item.Title;
            var category = item.Source?.Category ?? "General";
            var sourceName = item.Source?.Name ?? "Unknown";
            var summary = (draft?.MobileSummary ?? item.Summary ?? item.BodyText ?? "")
                .Length > 200 
                    ? (draft?.MobileSummary ?? item.Summary ?? item.BodyText ?? "")[..200] + "..."
                    : (draft?.MobileSummary ?? item.Summary ?? item.BodyText ?? "");

            basePrompt = template
                .Replace("{title}", title)
                .Replace("{category}", category)
                .Replace("{sourceName}", sourceName)
                .Replace("{summary}", summary);
        }

        // ALWAYS append safety clauses to prevent generating real faces, logos, etc.
        var safetyClauses = _aiOptions.SafetyClauses;
        if (!basePrompt.Contains(safetyClauses, StringComparison.OrdinalIgnoreCase))
        {
            basePrompt = $"{basePrompt} Style: {safetyClauses}";
        }

        return basePrompt;
    }

    /// <summary>
    /// Get primary image for content
    /// </summary>
    public async Task<MediaAsset?> GetPrimaryImageAsync(Guid contentItemId, CancellationToken ct = default)
    {
        return await _db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == contentItemId && l.IsPrimary)
            .Select(l => l.MediaAsset)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Get all media for content
    /// </summary>
    public async Task<List<MediaAssetDto>> GetMediaForContentAsync(Guid contentItemId, CancellationToken ct = default)
    {
        var links = await _db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == contentItemId)
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.SortOrder)
            .ToListAsync(ct);

        return links.Select(l => new MediaAssetDto(
            Id: l.MediaAsset.Id,
            Kind: l.MediaAsset.Kind,
            Origin: l.MediaAsset.Origin,
            SourceUrl: l.MediaAsset.SourceUrl,
            StoragePath: l.MediaAsset.StoragePath,
            ContentType: l.MediaAsset.ContentType,
            SizeBytes: l.MediaAsset.SizeBytes,
            Width: l.MediaAsset.Width,
            Height: l.MediaAsset.Height,
            AltText: l.MediaAsset.AltText,
            IsPrimary: l.IsPrimary,
            SortOrder: l.SortOrder,
            PublicUrl: _downloadService.GetPublicUrl(l.MediaAsset.StoragePath),
            CreatedAtUtc: l.MediaAsset.CreatedAtUtc
        )).ToList();
    }

    /// <summary>
    /// Set an asset as primary for content
    /// </summary>
    public async Task<bool> SetPrimaryAsync(Guid contentItemId, Guid assetId, CancellationToken ct = default)
    {
        // Unset current primary
        var currentPrimary = await _db.ContentMediaLinks
            .Where(l => l.ContentItemId == contentItemId && l.IsPrimary)
            .ToListAsync(ct);

        foreach (var link in currentPrimary)
        {
            link.IsPrimary = false;
        }

        // Set new primary
        var targetLink = await _db.ContentMediaLinks
            .FirstOrDefaultAsync(l => l.ContentItemId == contentItemId && l.MediaAssetId == assetId, ct);

        if (targetLink == null)
        {
            return false;
        }

        targetLink.IsPrimary = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Set asset {AssetId} as primary for content {ContentId}", assetId, contentItemId);
        return true;
    }

    /// <summary>
    /// Remove media link from content
    /// </summary>
    public async Task<bool> RemoveMediaAsync(Guid contentItemId, Guid assetId, CancellationToken ct = default)
    {
        var link = await _db.ContentMediaLinks
            .FirstOrDefaultAsync(l => l.ContentItemId == contentItemId && l.MediaAssetId == assetId, ct);

        if (link == null)
        {
            return false;
        }

        _db.ContentMediaLinks.Remove(link);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Removed media link {AssetId} from content {ContentId}", assetId, contentItemId);
        return true;
    }

    /// <summary>
    /// Get public URL for storage path
    /// </summary>
    public string GetPublicUrl(string storagePath) => _downloadService.GetPublicUrl(storagePath);
}

