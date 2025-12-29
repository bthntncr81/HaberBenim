using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Instagram;
using HaberPlatform.Api.Services.Media;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Publisher for Instagram - uses Meta Graph API
/// Requires Professional Instagram account connected to a Facebook Page
/// </summary>
public class InstagramPublisher : IChannelPublisher
{
    private readonly ILogger<InstagramPublisher> _logger;
    private readonly InstagramPublishingOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public string ChannelName => PublishChannels.Instagram;

    public InstagramPublisher(
        ILogger<InstagramPublisher> logger,
        IOptions<InstagramPublishingOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task<PublishResult> PublishAsync(ContentItem item, ContentDraft draft, CancellationToken ct = default)
    {
        try
        {
            // Build caption
            var caption = BuildCaption(item, draft);

            var requestPayload = new
            {
                contentItemId = item.Id,
                caption,
                captionLength = caption.Length
            };
            var requestJson = JsonSerializer.Serialize(requestPayload);

            // Check if publishing is enabled
            if (!_options.Enabled)
            {
                var stubResponse = new
                {
                    status = "stub",
                    stub = true,
                    connectorEnabled = false,
                    message = "Instagram connector is disabled (stub mode)",
                    timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Instagram post for content {ContentId} - stub mode (connector disabled)", item.Id);
                return PublishResult.Succeeded(requestJson, JsonSerializer.Serialize(stubResponse));
            }

            // Get services
            using var scope = _serviceProvider.CreateScope();
            var oauthService = scope.ServiceProvider.GetRequiredService<InstagramOAuthService>();
            var apiClient = scope.ServiceProvider.GetRequiredService<InstagramApiClient>();
            var alertService = scope.ServiceProvider.GetRequiredService<AlertService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mediaOptions = scope.ServiceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;

            // Get Instagram connection
            var connectionResult = await oauthService.GetDefaultPublisherAsync(ct);
            if (connectionResult == null)
            {
                _logger.LogWarning("No Instagram connection configured, skipping post");

                var noConnectionResponse = new
                {
                    status = "skipped",
                    reason = "No Instagram connection configured. Please connect via Settings.",
                    timestamp = DateTime.UtcNow
                };

                return PublishResult.Skipped(
                    "No Instagram connection configured",
                    requestJson,
                    JsonSerializer.Serialize(noConnectionResponse));
            }

            var (connection, pageAccessToken) = connectionResult.Value;

            // Get public asset base URL
            var publicBaseUrl = await oauthService.GetPublicAssetBaseUrlAsync();
            if (string.IsNullOrEmpty(publicBaseUrl))
            {
                _logger.LogError("PUBLIC_ASSET_BASE_URL not configured - required for Instagram publishing");

                await alertService.CreateAlertAsync(
                    type: "InstagramConfig",
                    severity: "Critical",
                    title: "Instagram publishing failed - PUBLIC_ASSET_BASE_URL not set",
                    message: "Instagram requires a publicly accessible HTTPS URL for images. Please configure PUBLIC_ASSET_BASE_URL in System Settings.",
                    meta: new { contentItemId = item.Id });

                return PublishResult.Failed(
                    "PUBLIC_ASSET_BASE_URL not configured - Instagram requires publicly accessible image URLs",
                    requestJson);
            }

            // Get primary image URL
            var imageUrl = await GetPrimaryImageUrlAsync(item.Id, publicBaseUrl, db, mediaOptions, ct);
            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogWarning("No image available for Instagram post {ContentId}", item.Id);

                var noImageResponse = new
                {
                    status = "skipped",
                    reason = "No image available for Instagram post. Instagram requires an image.",
                    timestamp = DateTime.UtcNow
                };

                return PublishResult.Skipped(
                    "No image available - Instagram requires an image",
                    requestJson,
                    JsonSerializer.Serialize(noImageResponse));
            }

            _logger.LogInformation("Publishing to Instagram @{Username}: image={ImageUrl}, caption={CaptionLength} chars",
                connection.IgUsername, imageUrl, caption.Length);

            // Publish
            var result = await apiClient.PublishImageAsync(
                connection.IgUserId,
                imageUrl,
                caption,
                pageAccessToken,
                ct);

            if (result.IsRateLimited)
            {
                _logger.LogWarning("Instagram rate limited for content {ContentId}", item.Id);

                await alertService.CreateAlertAsync(
                    type: "InstagramRateLimit",
                    severity: "Warn",
                    title: "Instagram rate limit reached",
                    message: $"Instagram publishing rate limited. Instagram allows 100 API-published posts per 24 hours.",
                    meta: new { contentItemId = item.Id, connectionId = connection.Id });

                var rateLimitResponse = new
                {
                    status = "rate_limited",
                    containerId = result.ContainerId,
                    error = result.Error,
                    timestamp = DateTime.UtcNow
                };

                return PublishResult.Failed(
                    "Instagram rate limit reached (100 posts/24h)",
                    requestJson,
                    JsonSerializer.Serialize(rateLimitResponse),
                    isRetryable: true,
                    retryAfter: DateTime.UtcNow.AddHours(1));
            }

            if (result.IsAuthError)
            {
                _logger.LogError("Instagram auth error for content {ContentId}", item.Id);

                await alertService.CreateAlertAsync(
                    type: "InstagramAuthFailed",
                    severity: "Critical",
                    title: "Instagram authentication failed - reconnect needed",
                    message: $"Failed to post to Instagram. The connection may have expired. Please reconnect via Settings.",
                    meta: new { 
                        contentItemId = item.Id, 
                        connectionId = connection.Id,
                        username = connection.IgUsername 
                    });

                return PublishResult.Failed(
                    "Instagram authentication failed - reconnect needed",
                    requestJson,
                    result.Error);
            }

            if (!result.Success)
            {
                _logger.LogError("Failed to post to Instagram for content {ContentId}: {Error}",
                    item.Id, result.Error);

                // Check for permission errors
                if (result.Error?.Contains("permission", StringComparison.OrdinalIgnoreCase) == true ||
                    result.Error?.Contains("task", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await alertService.CreateAlertAsync(
                        type: "InstagramPermission",
                        severity: "Critical",
                        title: "Instagram permission missing or Page task not granted",
                        message: $"Failed to post: {result.Error}. Ensure the Facebook Page has the required permissions/tasks for Instagram publishing.",
                        meta: new { contentItemId = item.Id, error = result.Error });
                }

                return PublishResult.Failed(
                    result.Error ?? "Unknown Instagram publishing error",
                    requestJson);
            }

            // Success!
            var successResponse = new
            {
                status = "success",
                stub = false,
                connectorEnabled = true,
                containerId = result.ContainerId,
                mediaId = result.MediaId,
                igUsername = connection.IgUsername,
                postUrl = $"https://www.instagram.com/p/{result.MediaId}/",
                caption = caption.Length > 100 ? caption[..100] + "..." : caption,
                imageUrl,
                timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Posted to Instagram @{Username}: media_id={MediaId}",
                connection.IgUsername, result.MediaId);

            return PublishResult.Succeeded(
                requestJson,
                JsonSerializer.Serialize(successResponse),
                externalId: result.MediaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Instagram post for content {ContentId}", item.Id);
            return PublishResult.Failed(ex.Message);
        }
    }

    private string BuildCaption(ContentItem item, ContentDraft draft)
    {
        // Use override if provided
        if (!string.IsNullOrEmpty(draft.InstagramCaptionOverride))
        {
            return TruncateCaption(draft.InstagramCaptionOverride);
        }

        // Build caption from content
        var parts = new List<string>();

        // Title
        var title = draft.WebTitle ?? item.Title;
        parts.Add(title);

        // Summary (if available and not too long)
        var summary = draft.MobileSummary ?? item.Summary;
        if (!string.IsNullOrEmpty(summary) && summary != title)
        {
            var shortSummary = summary.Length > 300 ? summary[..297] + "..." : summary;
            parts.Add(shortSummary);
        }

        // Source attribution
        if (item.Source != null)
        {
            parts.Add($"📰 {item.Source.Name}");
        }

        // Hashtags
        if (!string.IsNullOrEmpty(draft.HashtagsCsv))
        {
            var hashtags = string.Join(" ",
                draft.HashtagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.Trim().StartsWith('#') ? h.Trim() : $"#{h.Trim()}"));
            parts.Add(hashtags);
        }

        // Default hashtags from config
        if (!string.IsNullOrEmpty(_options.DefaultHashtags))
        {
            parts.Add(_options.DefaultHashtags);
        }

        var caption = string.Join("\n\n", parts);
        return TruncateCaption(caption);
    }

    private string TruncateCaption(string caption)
    {
        if (caption.Length <= _options.MaxCaptionLength)
        {
            return caption;
        }

        return caption[..(_options.MaxCaptionLength - 3)] + "...";
    }

    private async Task<string?> GetPrimaryImageUrlAsync(
        Guid contentItemId,
        string publicBaseUrl,
        AppDbContext db,
        MediaOptions mediaOptions,
        CancellationToken ct)
    {
        // Get primary media link
        var primaryLink = await db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == contentItemId && l.IsPrimary)
            .FirstOrDefaultAsync(ct);

        if (primaryLink?.MediaAsset != null)
        {
            var asset = primaryLink.MediaAsset;
            // Build public URL: PUBLIC_ASSET_BASE_URL + /media/ + storagePath
            var url = $"{publicBaseUrl.TrimEnd('/')}{mediaOptions.PublicBasePath}/{asset.StoragePath}";
            return url;
        }

        // Try to get any image
        var anyLink = await db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == contentItemId && l.MediaAsset.Kind == "Image")
            .OrderBy(l => l.SortOrder)
            .FirstOrDefaultAsync(ct);

        if (anyLink?.MediaAsset != null)
        {
            var asset = anyLink.MediaAsset;
            var url = $"{publicBaseUrl.TrimEnd('/')}{mediaOptions.PublicBasePath}/{asset.StoragePath}";
            return url;
        }

        return null;
    }
}

