using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Services.Video;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Orchestrates publishing content across all channels
/// </summary>
public class PublisherOrchestrator
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IChannelPublisher> _publishers;
    private readonly AiVideoService? _aiVideoService;
    private readonly ILogger<PublisherOrchestrator> _logger;

    public PublisherOrchestrator(
        AppDbContext db,
        IEnumerable<IChannelPublisher> publishers,
        ILogger<PublisherOrchestrator> logger,
        AiVideoService? aiVideoService = null)
    {
        _db = db;
        _publishers = publishers;
        _logger = logger;
        _aiVideoService = aiVideoService;
    }

    /// <summary>
    /// Publish content to all enabled channels (version-aware)
    /// </summary>
    public async Task<OrchestratorResult> PublishAsync(Guid contentId, int versionNo, Guid? publishedByUserId = null, CancellationToken ct = default)
    {
        var result = new OrchestratorResult { VersionNo = versionNo };

        // Load content with draft and source
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == contentId, ct);

        if (item == null)
        {
            result.Error = "Content item not found";
            return result;
        }

        if (item.Draft == null)
        {
            // Some ingestion paths (e.g. X) may create ContentItem without a draft.
            // Create a safe default draft so Web publishing can proceed.
            var draft = CreateDefaultDraft(item);
            item.Draft = draft;
            _db.ContentDrafts.Add(draft);
            _logger.LogInformation("Created default draft for content {ContentId} (Web only)", item.Id);
        }

        // Check if all channels are disabled
        if (!item.Draft.PublishToWeb && !item.Draft.PublishToMobile && !item.Draft.PublishToX && !item.Draft.PublishToInstagram)
        {
            result.Error = "No channels are enabled for publishing. At least one channel must be enabled.";
            _logger.LogWarning("Content {ContentId} has all channels disabled", contentId);
            return result;
        }

        var allEnabledSucceeded = true;
        var enabledChannelCount = 0;

        foreach (var publisher in _publishers)
        {
            // Check if this channel is enabled
            var isEnabled = IsChannelEnabled(item.Draft, publisher.ChannelName);
            
            if (!isEnabled)
            {
                // Log as skipped due to channel disabled
                var skipLog = new ChannelPublishLog
                {
                    Id = Guid.NewGuid(),
                    ContentItemId = item.Id,
                    Channel = publisher.ChannelName,
                    VersionNo = versionNo,
                    Status = ChannelPublishStatuses.Skipped,
                    CreatedAtUtc = DateTime.UtcNow,
                    ResponseJson = "{\"reason\": \"ChannelDisabled\"}"
                };
                _db.ChannelPublishLogs.Add(skipLog);
                
                result.ChannelResults[publisher.ChannelName] = new ChannelPublishResult
                {
                    Success = true,
                    Status = ChannelPublishStatuses.Skipped,
                    Reason = "ChannelDisabled"
                };

                _logger.LogInformation("Skipping {Channel} for content {ContentId} v{Version} - channel disabled", 
                    publisher.ChannelName, item.Id, versionNo);
                continue;
            }

            enabledChannelCount++;
            var channelResult = await PublishToChannelAsync(item, item.Draft, publisher, versionNo, ct);
            result.ChannelResults[publisher.ChannelName] = channelResult;

            if (!channelResult.Success && channelResult.Status != ChannelPublishStatuses.Skipped)
            {
                allEnabledSucceeded = false;
            }
        }

        await _db.SaveChangesAsync(ct);

        // If all enabled channels succeeded (or skipped due to already published), mark as Published
        if (allEnabledSucceeded && enabledChannelCount > 0)
        {
            item.Status = ContentStatuses.Published;
            item.PublishedByUserId = publishedByUserId;
            await _db.SaveChangesAsync(ct);
            result.AllSucceeded = true;
            _logger.LogInformation("Content {ContentId} v{Version} published to all enabled channels", contentId, versionNo);

            // Trigger AI video generation if enabled
            await TriggerAiVideoGenerationAsync(item, ct);
        }
        else if (enabledChannelCount == 0)
        {
            result.Error = "No enabled channels were found for publishing";
        }
        else
        {
            _logger.LogWarning("Content {ContentId} v{Version} failed to publish to some channels", contentId, versionNo);
        }

        return result;
    }

    /// <summary>
    /// Trigger AI video generation after successful publish if enabled in draft
    /// </summary>
    private async Task TriggerAiVideoGenerationAsync(ContentItem item, CancellationToken ct)
    {
        if (_aiVideoService == null || !_aiVideoService.IsEnabled)
        {
            return;
        }

        if (item.Draft?.GenerateAiVideo != true)
        {
            _logger.LogDebug("AI video generation not enabled for content {ContentId}", item.Id);
            return;
        }

        try
        {
            var request = new AiVideoGenerateRequest(
                Force: false,
                Mode: item.Draft.AiVideoMode ?? AiVideoMode.AutoPrompt,
                PromptOverride: item.Draft.AiVideoPromptOverride
            );

            var job = await _aiVideoService.GenerateAsync(item.Id, request, ct);
            _logger.LogInformation("Triggered AI video generation for content {ContentId}, job: {JobId}", 
                item.Id, job.Id);
        }
        catch (Exception ex)
        {
            // Don't fail the publish if video generation fails
            _logger.LogWarning(ex, "Failed to trigger AI video generation for content {ContentId}", item.Id);
        }
    }

    /// <summary>
    /// Publish content using the current version (for backward compatibility)
    /// </summary>
    public async Task<OrchestratorResult> PublishAsync(Guid contentId, CancellationToken ct = default)
    {
        // Load item to get current version
        var item = await _db.ContentItems.FindAsync([contentId], ct);
        var versionNo = item?.CurrentVersionNo ?? 1;
        return await PublishAsync(contentId, versionNo, null, ct);
    }

    private bool IsChannelEnabled(ContentDraft draft, string channelName)
    {
        return channelName switch
        {
            PublishChannels.Web => draft.PublishToWeb,
            PublishChannels.Mobile => draft.PublishToMobile,
            PublishChannels.X => draft.PublishToX,
            PublishChannels.Instagram => draft.PublishToInstagram,
            _ => true
        };
    }

    private async Task<ChannelPublishResult> PublishToChannelAsync(
        ContentItem item,
        ContentDraft draft,
        IChannelPublisher publisher,
        int versionNo,
        CancellationToken ct)
    {
        var channelName = publisher.ChannelName;

        // Check for existing success log for this specific version (version-aware idempotency)
        var existingSuccess = await _db.ChannelPublishLogs
            .AnyAsync(l => l.ContentItemId == item.Id 
                && l.Channel == channelName 
                && l.VersionNo == versionNo
                && l.Status == ChannelPublishStatuses.Success, ct);

        if (existingSuccess)
        {
            // Create skipped log
            var skipLog = new ChannelPublishLog
            {
                Id = Guid.NewGuid(),
                ContentItemId = item.Id,
                Channel = channelName,
                VersionNo = versionNo,
                Status = ChannelPublishStatuses.Skipped,
                CreatedAtUtc = DateTime.UtcNow,
                ResponseJson = "{\"reason\": \"AlreadyPublished\"}"
            };
            _db.ChannelPublishLogs.Add(skipLog);

            _logger.LogInformation("Skipping {Channel} for content {ContentId} v{Version} - already published", 
                channelName, item.Id, versionNo);

            return new ChannelPublishResult
            {
                Success = true,
                Status = ChannelPublishStatuses.Skipped,
                Reason = "AlreadyPublished"
            };
        }

        // Attempt to publish
        var result = await publisher.PublishAsync(item, draft, ct);

        // Determine status based on result
        var status = result.IsSkipped 
            ? ChannelPublishStatuses.Skipped 
            : result.Success 
                ? ChannelPublishStatuses.Success 
                : ChannelPublishStatuses.Failed;

        // Create log with version
        var log = new ChannelPublishLog
        {
            Id = Guid.NewGuid(),
            ContentItemId = item.Id,
            Channel = channelName,
            VersionNo = versionNo,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            RequestJson = result.RequestJson,
            ResponseJson = result.ResponseJson,
            Error = result.Error,
            ExternalPostId = result.ExternalId
        };
        _db.ChannelPublishLogs.Add(log);

        return new ChannelPublishResult
        {
            Success = result.Success,
            Status = status,
            Error = result.Error,
            Reason = result.SkipReason,
            IsRetryable = result.IsRetryable,
            RetryAfter = result.RetryAfter,
            ExternalId = result.ExternalId
        };
    }

    private static ContentDraft CreateDefaultDraft(ContentItem item)
    {
        // IMPORTANT: Keep this conservative. If we enable X by default and X credentials are missing,
        // the whole orchestrator run will be considered partially failed and Web won't get published.
        return new ContentDraft
        {
            Id = Guid.NewGuid(),
            ContentItemId = item.Id,
            XText = TruncateText(item.Title, 280),
            WebTitle = item.Title,
            WebBody = item.BodyText,
            MobileSummary = TruncateText(item.Summary ?? item.BodyText, 200),
            PushTitle = TruncateText(item.Title, 100),
            PushBody = TruncateText(item.Summary ?? item.BodyText, 200),
            PublishToWeb = true,
            PublishToMobile = false,
            PublishToX = false,
            PublishToInstagram = false,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

public class OrchestratorResult
{
    public bool AllSucceeded { get; set; }
    public int VersionNo { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, ChannelPublishResult> ChannelResults { get; set; } = new();
}

public class ChannelPublishResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Error { get; set; }
    public bool IsRetryable { get; set; }
    public DateTime? RetryAfter { get; set; }
    public string? ExternalId { get; set; }
}
