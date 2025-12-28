using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Orchestrates publishing content across all channels
/// </summary>
public class PublisherOrchestrator
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IChannelPublisher> _publishers;
    private readonly ILogger<PublisherOrchestrator> _logger;

    public PublisherOrchestrator(
        AppDbContext db,
        IEnumerable<IChannelPublisher> publishers,
        ILogger<PublisherOrchestrator> logger)
    {
        _db = db;
        _publishers = publishers;
        _logger = logger;
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
            result.Error = "Content item has no draft";
            return result;
        }

        // Check if all channels are disabled
        if (!item.Draft.PublishToWeb && !item.Draft.PublishToMobile && !item.Draft.PublishToX)
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
