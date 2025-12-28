using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Service for managing publish jobs
/// </summary>
public class PublishJobService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PublishJobService> _logger;

    public PublishJobService(AppDbContext db, ILogger<PublishJobService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Enqueue a publish job for the given content item (version-aware)
    /// </summary>
    public async Task<EnqueueResult> EnqueueAsync(
        Guid contentId, 
        Guid? userId = null, 
        int? versionNo = null, 
        DateTime? scheduledAtUtc = null,
        string? publishOrigin = null,
        CancellationToken ct = default)
    {
        var item = await _db.ContentItems.FindAsync([contentId], ct);
        if (item == null)
        {
            return new EnqueueResult { Success = false, Error = "Content item not found" };
        }

        // Determine scheduled time
        DateTime effectiveScheduledAt;
        if (scheduledAtUtc.HasValue)
        {
            effectiveScheduledAt = scheduledAtUtc.Value;
        }
        else if (item.Status == ContentStatuses.Scheduled && item.ScheduledAtUtc.HasValue)
        {
            effectiveScheduledAt = item.ScheduledAtUtc.Value;
        }
        else if (item.Status == ContentStatuses.ReadyToPublish || item.Status == ContentStatuses.AutoReady || item.Status == ContentStatuses.Published)
        {
            effectiveScheduledAt = DateTime.UtcNow;
        }
        else
        {
            return new EnqueueResult 
            { 
                Success = false, 
                Error = $"Content status '{item.Status}' is not eligible for publishing" 
            };
        }

        // Determine version - use provided version or current version
        var effectiveVersionNo = versionNo ?? item.CurrentVersionNo;

        // Check for existing active job for the same version
        var existingActiveJob = await _db.PublishJobs
            .AnyAsync(j => j.ContentItemId == contentId 
                && j.VersionNo == effectiveVersionNo
                && (j.Status == PublishJobStatuses.Pending || j.Status == PublishJobStatuses.Running), ct);

        if (existingActiveJob)
        {
            return new EnqueueResult 
            { 
                Success = true, 
                AlreadyQueued = true,
                VersionNo = effectiveVersionNo,
                Message = "Publish job already queued for this version" 
            };
        }

        // Update publish origin if provided
        if (!string.IsNullOrEmpty(publishOrigin))
        {
            item.PublishOrigin = publishOrigin;
        }

        // Create new job with version
        var job = new PublishJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentId,
            ScheduledAtUtc = effectiveScheduledAt,
            VersionNo = effectiveVersionNo,
            Status = PublishJobStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _db.PublishJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Enqueued publish job {JobId} for content {ContentId} v{Version} scheduled at {ScheduledAt}",
            job.Id, contentId, effectiveVersionNo, effectiveScheduledAt);

        return new EnqueueResult
        {
            Success = true,
            JobId = job.Id,
            VersionNo = effectiveVersionNo,
            ScheduledAtUtc = effectiveScheduledAt,
            Message = "Publish job enqueued successfully"
        };
    }

    /// <summary>
    /// Cancel pending jobs for a content item
    /// </summary>
    public async Task<int> CancelPendingJobsAsync(Guid contentId, CancellationToken ct = default)
    {
        var pendingJobs = await _db.PublishJobs
            .Where(j => j.ContentItemId == contentId && j.Status == PublishJobStatuses.Pending)
            .ToListAsync(ct);

        foreach (var job in pendingJobs)
        {
            job.Status = PublishJobStatuses.Cancelled;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled {Count} pending jobs for content {ContentId}", 
            pendingJobs.Count, contentId);

        return pendingJobs.Count;
    }
}

public class EnqueueResult
{
    public bool Success { get; set; }
    public bool AlreadyQueued { get; set; }
    public Guid? JobId { get; set; }
    public int VersionNo { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

