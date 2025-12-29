using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Media;

/// <summary>
/// Downloads and stores media files from URLs
/// </summary>
public class MediaDownloadService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly MediaOptions _options;
    private readonly ILogger<MediaDownloadService> _logger;

    public MediaDownloadService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<MediaOptions> options,
        ILogger<MediaDownloadService> logger)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient("MediaDownload");
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Download image from URL, validate, store, and create MediaAsset
    /// </summary>
    public async Task<MediaDownloadResult> DownloadAndStoreAsync(
        MediaCandidate candidate,
        Guid contentItemId,
        bool setPrimary = true,
        CancellationToken ct = default)
    {
        try
        {
            // Download image
            using var request = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
            request.Headers.Add("User-Agent", "HaberPlatform/1.0 (Media Fetch)");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                return MediaDownloadResult.Failed($"HTTP {(int)response.StatusCode} from {candidate.Url}");
            }

            // Check content type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            if (!_options.AllowedContentTypes.Contains(contentType))
            {
                return MediaDownloadResult.Failed($"Unsupported content type: {contentType}");
            }

            // Check size
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            if (contentLength > _options.MaxFileSizeBytes)
            {
                return MediaDownloadResult.Failed($"File too large: {contentLength} bytes (max {_options.MaxFileSizeBytes})");
            }

            // Read bytes
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            if (bytes.Length > _options.MaxFileSizeBytes)
            {
                return MediaDownloadResult.Failed($"File too large: {bytes.Length} bytes");
            }

            // Compute hash for deduplication
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            // Check for existing asset with same hash
            var existingAsset = await _db.MediaAssets
                .Where(a => a.Sha256 == sha256)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(ct);

            if (existingAsset != Guid.Empty)
            {
                // Link existing asset to content
                await LinkAssetToContentAsync(existingAsset, contentItemId, setPrimary, ct);
                
                _logger.LogInformation("Reused existing media asset {AssetId} (hash match) for content {ContentId}",
                    existingAsset, contentItemId);

                return MediaDownloadResult.Succeeded(existingAsset, "");
            }

            // Validate and get dimensions using ImageSharp
            int width, height;
            try
            {
                ms.Position = 0;
                using var image = await Image.LoadAsync(ms, ct);
                width = image.Width;
                height = image.Height;
            }
            catch (Exception ex)
            {
                return MediaDownloadResult.Failed($"Invalid image format: {ex.Message}");
            }

            // Determine extension from content type
            var extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".bin"
            };

            // Create asset ID and storage path
            var assetId = Guid.NewGuid();
            var storagePath = $"{assetId}{extension}";
            var fullPath = Path.Combine(GetAbsoluteRootDir(), storagePath);

            // Ensure directory exists
            Directory.CreateDirectory(GetAbsoluteRootDir());

            // Write file
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            // Create MediaAsset entity
            var asset = new MediaAsset
            {
                Id = assetId,
                Kind = MediaKinds.Image,
                Origin = candidate.Origin,
                SourceUrl = candidate.Url,
                StoragePath = storagePath,
                ContentType = contentType,
                SizeBytes = bytes.Length,
                Width = width,
                Height = height,
                Sha256 = sha256,
                AltText = candidate.AltText,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.MediaAssets.Add(asset);

            // Link to content
            await LinkAssetToContentAsync(assetId, contentItemId, setPrimary, ct, saveChanges: false);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Downloaded and stored media asset {AssetId} ({Width}x{Height}) for content {ContentId}",
                assetId, width, height, contentItemId);

            return MediaDownloadResult.Succeeded(assetId, storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download media from {Url} for content {ContentId}",
                candidate.Url, contentItemId);
            return MediaDownloadResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Store bytes directly as a media asset (for AI-generated images)
    /// </summary>
    public async Task<MediaDownloadResult> StoreFromBytesAsync(
        byte[] bytes,
        string contentType,
        int width,
        int height,
        Guid contentItemId,
        string origin,
        string? generationPrompt = null,
        bool setPrimary = true,
        CancellationToken ct = default)
    {
        try
        {
            // Compute hash
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            // Determine extension
            var extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".png"
            };

            // Create asset
            var assetId = Guid.NewGuid();
            var storagePath = $"{assetId}{extension}";
            var fullPath = Path.Combine(GetAbsoluteRootDir(), storagePath);

            // Ensure directory exists
            Directory.CreateDirectory(GetAbsoluteRootDir());

            // Write file
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            var asset = new MediaAsset
            {
                Id = assetId,
                Kind = MediaKinds.Image,
                Origin = origin,
                SourceUrl = null,
                StoragePath = storagePath,
                ContentType = contentType,
                SizeBytes = bytes.Length,
                Width = width,
                Height = height,
                Sha256 = sha256,
                GenerationPrompt = generationPrompt,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.MediaAssets.Add(asset);

            // Link to content
            await LinkAssetToContentAsync(assetId, contentItemId, setPrimary, ct, saveChanges: false);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Stored media asset {AssetId} ({Width}x{Height}, origin={Origin}) for content {ContentId}",
                assetId, width, height, origin, contentItemId);

            return MediaDownloadResult.Succeeded(assetId, storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store media bytes for content {ContentId}", contentItemId);
            return MediaDownloadResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Link an existing asset to a content item
    /// </summary>
    public async Task LinkAssetToContentAsync(
        Guid assetId,
        Guid contentItemId,
        bool setPrimary,
        CancellationToken ct,
        bool saveChanges = true)
    {
        // Check if link already exists
        var existingLink = await _db.ContentMediaLinks
            .AnyAsync(l => l.ContentItemId == contentItemId && l.MediaAssetId == assetId, ct);

        if (existingLink)
        {
            return;
        }

        // If setting as primary, unset other primaries first
        if (setPrimary)
        {
            var existingPrimary = await _db.ContentMediaLinks
                .Where(l => l.ContentItemId == contentItemId && l.IsPrimary)
                .ToListAsync(ct);

            foreach (var link in existingPrimary)
            {
                link.IsPrimary = false;
            }
        }

        // Get next sort order
        var maxOrder = await _db.ContentMediaLinks
            .Where(l => l.ContentItemId == contentItemId)
            .MaxAsync(l => (int?)l.SortOrder, ct) ?? -1;

        var newLink = new ContentMediaLink
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentItemId,
            MediaAssetId = assetId,
            IsPrimary = setPrimary,
            SortOrder = maxOrder + 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ContentMediaLinks.Add(newLink);

        if (saveChanges)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Get absolute path to media root directory
    /// </summary>
    public string GetAbsoluteRootDir()
    {
        var rootDir = _options.RootDir;
        if (!Path.IsPathRooted(rootDir))
        {
            rootDir = Path.Combine(Directory.GetCurrentDirectory(), rootDir);
        }
        return rootDir;
    }

    /// <summary>
    /// Get public URL for a storage path
    /// </summary>
    public string GetPublicUrl(string storagePath)
    {
        return $"{_options.PublicBasePath}/{storagePath}";
    }
}

