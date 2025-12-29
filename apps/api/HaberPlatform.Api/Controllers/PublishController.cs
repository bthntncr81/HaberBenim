using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Publishing;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/publish")]
[Authorize(Roles = "Admin,Editor")]
public class PublishController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PublishJobService _jobService;
    private readonly PublishJobWorker _jobWorker;
    private readonly ILogger<PublishController> _logger;

    public PublishController(
        AppDbContext db,
        PublishJobService jobService,
        PublishJobWorker jobWorker,
        ILogger<PublishController> logger)
    {
        _db = db;
        _jobService = jobService;
        _jobWorker = jobWorker;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Enqueue a publish job for a content item
    /// </summary>
    [HttpPost("enqueue/{contentId:guid}")]
    public async Task<ActionResult<EnqueueResponse>> Enqueue(Guid contentId)
    {
        var userId = GetCurrentUserId();
        var result = await _jobService.EnqueueAsync(contentId, userId);

        return Ok(new EnqueueResponse(
            result.Success,
            result.AlreadyQueued,
            result.JobId,
            result.VersionNo,
            result.ScheduledAtUtc,
            result.Message,
            result.Error
        ));
    }

    /// <summary>
    /// Manually trigger processing of due jobs (Admin only)
    /// </summary>
    [HttpPost("run-due")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RunDueResponse>> RunDue()
    {
        var processed = await _jobWorker.ProcessDueJobsAsync();

        return Ok(new RunDueResponse(processed, $"Processed {processed} due jobs"));
    }

    /// <summary>
    /// Force retry publishing for a content item by resetting any Pending/Failed jobs to be due now (Admin only)
    /// </summary>
    [HttpPost("force-retry/{contentId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ForceRetry(Guid contentId)
    {
        if (contentId == Guid.Empty)
            return BadRequest(new { error = "contentId is required" });

        var now = DateTime.UtcNow;

        var updated = await _db.PublishJobs
            .Where(j => j.ContentItemId == contentId && (j.Status == PublishJobStatuses.Pending || j.Status == PublishJobStatuses.Failed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, PublishJobStatuses.Pending)
                .SetProperty(j => j.ScheduledAtUtc, now)
                .SetProperty(j => j.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(j => j.LastError, (string?)null)
                .SetProperty(j => j.AttemptCount, 0));

        var processed = await _jobWorker.ProcessDueJobsAsync();

        return Ok(new
        {
            message = "Force retry completed",
            updatedJobs = updated,
            jobsProcessed = processed
        });
    }

    /// <summary>
    /// Reset jobs that previously failed due to missing drafts (Admin only)
    /// Useful after fixing ingestion/publishing logic.
    /// </summary>
    [HttpPost("reset-no-draft-jobs")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ResetNoDraftJobs()
    {
        var now = DateTime.UtcNow;

        var updated = await _db.PublishJobs
            .Where(j =>
                j.LastError != null &&
                (j.Status == PublishJobStatuses.Pending || j.Status == PublishJobStatuses.Failed) &&
                j.LastError.Contains("no draft"))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, PublishJobStatuses.Pending)
                .SetProperty(j => j.ScheduledAtUtc, now)
                .SetProperty(j => j.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(j => j.LastError, (string?)null)
                .SetProperty(j => j.AttemptCount, 0));

        var processed = await _jobWorker.ProcessDueJobsAsync();

        return Ok(new
        {
            message = "Reset no-draft jobs completed",
            updatedJobs = updated,
            jobsProcessed = processed
        });
    }

    /// <summary>
    /// Publish all AutoReady content items to Web (Admin only)
    /// Creates PublishJobs for all AutoReady items and processes them immediately
    /// </summary>
    [HttpPost("publish-all-ready")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> PublishAllReady()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        // Find all AutoReady items that don't have a PublishedContent yet
        var autoReadyItems = await _db.ContentItems
            .Where(c => c.Status == ContentStatuses.AutoReady)
            .Where(c => !_db.PublishedContents.Any(p => p.ContentItemId == c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        if (autoReadyItems.Count == 0)
        {
            return Ok(new { 
                message = "No AutoReady items to publish", 
                enqueued = 0, 
                processed = 0 
            });
        }

        _logger.LogInformation("Publishing {Count} AutoReady items", autoReadyItems.Count);

        var enqueued = 0;
        var errors = new List<string>();

        // Create PublishJobs for each
        foreach (var contentId in autoReadyItems)
        {
            try
            {
                var result = await _jobService.EnqueueAsync(contentId, userId);
                if (result.Success || result.AlreadyQueued)
                {
                    enqueued++;
                }
                else if (!string.IsNullOrEmpty(result.Error))
                {
                    errors.Add($"{contentId}: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{contentId}: {ex.Message}");
            }
        }

        // Process jobs immediately
        var processed = await _jobWorker.ProcessDueJobsAsync();

        _logger.LogInformation("Published {Enqueued} items, processed {Processed} jobs", enqueued, processed);

        return Ok(new { 
            message = $"Published {processed} items to Web",
            found = autoReadyItems.Count,
            enqueued,
            processed,
            errors = errors.Count > 0 ? errors : null
        });
    }

    /// <summary>
    /// List publish jobs with filters
    /// </summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<PublishJobListResponse>> GetJobs([FromQuery] PublishJobQueryParams query)
    {
        var q = _db.PublishJobs
            .Include(j => j.ContentItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(j => j.Status == query.Status);
        }

        if (query.FromUtc.HasValue)
        {
            q = q.Where(j => j.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            q = q.Where(j => j.CreatedAtUtc <= query.ToUtc.Value);
        }

        var total = await q.CountAsync();

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var jobs = await q
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new PublishJobDto(
                j.Id,
                j.ContentItemId,
                j.ContentItem.Title,
                j.ScheduledAtUtc,
                j.VersionNo,
                j.Status,
                j.AttemptCount,
                j.LastAttemptAtUtc,
                j.NextRetryAtUtc,
                j.LastError,
                j.CreatedAtUtc
            ))
            .ToListAsync();

        return Ok(new PublishJobListResponse(jobs, total, page, pageSize));
    }

    /// <summary>
    /// Get publish logs for a specific content item
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<List<ChannelPublishLogDto>>> GetLogs([FromQuery] Guid contentId)
    {
        if (contentId == Guid.Empty)
        {
            return BadRequest(new { error = "contentId is required" });
        }

        var logs = await _db.ChannelPublishLogs
            .Where(l => l.ContentItemId == contentId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new ChannelPublishLogDto(
                l.Id,
                l.Channel,
                l.VersionNo,
                l.Status,
                l.CreatedAtUtc,
                l.RequestJson,
                l.ResponseJson,
                l.Error
            ))
            .ToListAsync();

        return Ok(logs);
    }
}

