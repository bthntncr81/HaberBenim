using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HaberPlatform.Api.Services;

/// <summary>
/// Service for breaking news operations
/// </summary>
public class BreakingNewsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BreakingNewsService> _logger;

    public BreakingNewsService(AppDbContext db, ILogger<BreakingNewsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Mark content as breaking news
    /// </summary>
    public async Task<MarkBreakingResponse> MarkAsBreakingAsync(
        Guid contentId,
        MarkBreakingRequest request,
        Guid userId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null)
        {
            return new MarkBreakingResponse { Ok = false, Error = "Content not found" };
        }

        // Validate push requirement - if BreakingPushRequired is true but PublishToMobile is false, fail
        var pushRequired = request.PushRequired ?? true;
        if (pushRequired && item.Draft != null && !item.Draft.PublishToMobile)
        {
            return new MarkBreakingResponse
            {
                Ok = false,
                Error = "Breaking news requires push notification, but PublishToMobile is disabled. Enable mobile publishing or set pushRequired=false."
            };
        }

        // Mark as breaking
        item.IsBreaking = true;
        item.BreakingAtUtc = DateTime.UtcNow;
        item.BreakingByUserId = userId;
        item.BreakingNote = request.Note;
        item.BreakingPriority = request.Priority ?? 100;
        item.BreakingPushRequired = pushRequired;

        // Fast-track to ReadyToPublish
        item.Status = "ReadyToPublish";
        item.DecisionType = "Breaking";
        item.DecisionReason = "Marked as breaking news";
        item.DecidedAtUtc = DateTime.UtcNow;

        // Increment version
        item.CurrentVersionNo++;

        // Create revision
        var revision = new ContentRevision
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentId,
            VersionNo = item.CurrentVersionNo,
            ActionType = "BreakingMarked",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                breakingNote = request.Note,
                breakingPriority = item.BreakingPriority,
                pushRequired
            })
        };
        _db.ContentRevisions.Add(revision);

        // Create publish job (scheduled for NOW)
        var job = new PublishJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentId,
            ScheduledAtUtc = DateTime.UtcNow,
            VersionNo = item.CurrentVersionNo,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _db.PublishJobs.Add(job);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Content {ContentId} marked as breaking news by user {UserId}, job {JobId} created",
            contentId, userId, job.Id);

        return new MarkBreakingResponse
        {
            Ok = true,
            VersionNo = item.CurrentVersionNo,
            JobId = job.Id
        };
    }

    /// <summary>
    /// Re-enqueue publish job for breaking content
    /// </summary>
    public async Task<MarkBreakingResponse> PublishNowAsync(Guid contentId, Guid userId)
    {
        var item = await _db.ContentItems.FindAsync(contentId);

        if (item == null)
        {
            return new MarkBreakingResponse { Ok = false, Error = "Content not found" };
        }

        if (!item.IsBreaking)
        {
            return new MarkBreakingResponse { Ok = false, Error = "Content is not marked as breaking" };
        }

        // Create new publish job
        var job = new PublishJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentId,
            ScheduledAtUtc = DateTime.UtcNow,
            VersionNo = item.CurrentVersionNo,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _db.PublishJobs.Add(job);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Breaking content {ContentId} republish job {JobId} created by user {UserId}",
            contentId, job.Id, userId);

        return new MarkBreakingResponse
        {
            Ok = true,
            VersionNo = item.CurrentVersionNo,
            JobId = job.Id
        };
    }

    /// <summary>
    /// Get breaking news inbox
    /// </summary>
    public async Task<BreakingInboxResponse> GetBreakingInboxAsync(BreakingInboxParams queryParams)
    {
        var query = _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Where(c => c.IsBreaking);

        if (!string.IsNullOrEmpty(queryParams.Status))
        {
            query = query.Where(c => c.Status == queryParams.Status);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.BreakingPriority)
            .ThenByDescending(c => c.BreakingAtUtc)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(c => new BreakingInboxItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Summary = c.Summary,
                SourceName = c.Source.Name,
                Status = c.Status,
                BreakingAtUtc = c.BreakingAtUtc!.Value,
                BreakingNote = c.BreakingNote,
                BreakingPriority = c.BreakingPriority,
                BreakingPushRequired = c.BreakingPushRequired,
                BreakingByUserId = c.BreakingByUserId,
                PublishedAtUtc = c.PublishedAtUtc,
                HasDraft = c.Draft != null
            })
            .ToListAsync();

        return new BreakingInboxResponse
        {
            Items = items,
            Total = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }
}

