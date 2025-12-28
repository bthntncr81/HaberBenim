using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using HaberPlatform.Api.Services.Publishing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/editorial")]
[Authorize]
public class EditorialController : ControllerBase
{
    private readonly EditorialService _editorialService;
    private readonly PublishJobService _publishJobService;
    private readonly AlertService _alertService;
    private readonly AppDbContext _db;
    private readonly ILogger<EditorialController> _logger;

    public EditorialController(
        EditorialService editorialService, 
        PublishJobService publishJobService,
        AlertService alertService,
        AppDbContext db,
        ILogger<EditorialController> logger)
    {
        _editorialService = editorialService;
        _publishJobService = publishJobService;
        _alertService = alertService;
        _db = db;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get editorial inbox items
    /// </summary>
    [HttpGet("inbox")]
    public async Task<ActionResult<EditorialInboxResponse>> GetInbox(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? sourceId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new EditorialInboxQuery
        {
            Status = status,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            SourceId = sourceId,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };

        var result = await _editorialService.GetInboxAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Get full editorial item details
    /// </summary>
    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult<EditorialItemDto>> GetItem(Guid id)
    {
        var item = await _editorialService.GetItemAsync(id);
        if (item == null)
            return NotFound(new { error = "Content item not found" });

        return Ok(item);
    }

    /// <summary>
    /// Save or update draft for a content item
    /// </summary>
    [HttpPut("items/{id:guid}/draft")]
    [Authorize(Roles = "Admin,Editor,SocialMedia")]
    public async Task<ActionResult<SaveDraftResponse>> SaveDraft(Guid id, [FromBody] SaveDraftRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _editorialService.SaveDraftAsync(id, request, userId);
        if (result == null)
            return NotFound(new { error = "Content item not found" });

        return Ok(result);
    }

    /// <summary>
    /// Approve content item (set status to ReadyToPublish) and enqueue publish job
    /// </summary>
    [HttpPost("items/{id:guid}/approve")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult> Approve(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var success = await _editorialService.ApproveAsync(id, userId);
        if (!success)
            return NotFound(new { error = "Content item not found" });

        // Auto-enqueue publish job (immediate)
        var enqueueResult = await _publishJobService.EnqueueAsync(id, userId);
        
        _logger.LogInformation("Content {ContentId} approved and enqueue result: {Message}", 
            id, enqueueResult.Message);

        return Ok(new { 
            message = "Content approved and queued for publishing", 
            jobId = enqueueResult.JobId,
            alreadyQueued = enqueueResult.AlreadyQueued
        });
    }

    /// <summary>
    /// Reject content item
    /// </summary>
    [HttpPost("items/{id:guid}/reject")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] RejectRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var success = await _editorialService.RejectAsync(id, request.Reason, userId);
        if (!success)
            return NotFound(new { error = "Content item not found" });

        return Ok(new { message = "Content rejected successfully" });
    }

    /// <summary>
    /// Schedule content item for future publishing and enqueue publish job
    /// </summary>
    [HttpPost("items/{id:guid}/schedule")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult> Schedule(Guid id, [FromBody] ScheduleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.ScheduledAtUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Scheduled time must be in the future" });

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var success = await _editorialService.ScheduleAsync(id, request.ScheduledAtUtc, userId);
        if (!success)
            return NotFound(new { error = "Content item not found" });

        // Get the current version after scheduling
        var versionNo = await _editorialService.GetCurrentVersionNoAsync(id);

        // Auto-enqueue publish job for scheduled time with version
        var enqueueResult = await _publishJobService.EnqueueAsync(
            id, userId, versionNo, request.ScheduledAtUtc, Entities.PublishOrigins.Editorial);
        
        _logger.LogInformation("Content {ContentId} v{Version} scheduled for {ScheduledAt} and enqueue result: {Message}", 
            id, versionNo, request.ScheduledAtUtc, enqueueResult.Message);

        return Ok(new { 
            message = "Content scheduled and queued for publishing", 
            scheduledAtUtc = request.ScheduledAtUtc,
            versionNo = versionNo,
            jobId = enqueueResult.JobId,
            alreadyQueued = enqueueResult.AlreadyQueued
        });
    }

    /// <summary>
    /// Correct a published content item (Sprint 7)
    /// Only allowed for Published status. Creates a new version and re-publishes.
    /// </summary>
    [HttpPost("items/{id:guid}/correct")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<CorrectionResponse>> Correct(Guid id, [FromBody] CorrectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        // If correction note is provided, include it in editorial note
        if (!string.IsNullOrEmpty(request.CorrectionNote))
        {
            request.EditorialNote = string.IsNullOrEmpty(request.EditorialNote)
                ? $"[Correction] {request.CorrectionNote}"
                : $"{request.EditorialNote}\n[Correction] {request.CorrectionNote}";
        }

        var result = await _editorialService.CorrectAsync(id, request, userId);
        if (result == null)
            return NotFound(new { error = "Content item not found" });

        if (!result.Success)
            return BadRequest(new CorrectionResponse(false, 0, null, result.Error));

        // Enqueue a publish job immediately with the new version
        var enqueueResult = await _publishJobService.EnqueueAsync(
            id, userId, result.VersionNo, DateTime.UtcNow, Entities.PublishOrigins.Editorial);

        _logger.LogInformation("Content {ContentId} corrected to v{Version} and enqueue result: {Message}", 
            id, result.VersionNo, enqueueResult.Message);

        return Ok(new CorrectionResponse(
            true, 
            result.VersionNo, 
            enqueueResult.JobId,
            null
        ));
    }

    /// <summary>
    /// Retract (takedown) a published content item (Sprint 8)
    /// Only allowed for Published status.
    /// </summary>
    [HttpPost("items/{id:guid}/retract")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<RetractResponse>> Retract(Guid id, [FromBody] RetractRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new RetractResponse { Ok = false, Error = "Retract reason is required" });

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var item = await _db.ContentItems
            .Include(c => c.PublishedContent)
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null)
            return NotFound(new RetractResponse { Ok = false, Error = "Content not found" });

        if (item.Status != "Published")
            return BadRequest(new RetractResponse { Ok = false, Error = "Only published content can be retracted" });

        // Set retract fields on ContentItem
        item.RetractedAtUtc = DateTime.UtcNow;
        item.RetractedByUserId = userId;
        item.RetractReason = request.Reason;
        item.Status = "Retracted";

        // Increment version
        item.CurrentVersionNo++;

        // Create revision
        var revision = new ContentRevision
        {
            Id = Guid.NewGuid(),
            ContentItemId = id,
            VersionNo = item.CurrentVersionNo,
            ActionType = "Retracted",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            SnapshotJson = JsonSerializer.Serialize(new { reason = request.Reason })
        };
        _db.ContentRevisions.Add(revision);

        // Soft-delete published content
        if (item.PublishedContent != null)
        {
            item.PublishedContent.IsRetracted = true;
            item.PublishedContent.RetractedAtUtc = DateTime.UtcNow;
        }

        // Create channel publish logs for retraction
        var channels = new[] { "Web", "Mobile", "X" };
        foreach (var channel in channels)
        {
            _db.ChannelPublishLogs.Add(new ChannelPublishLog
            {
                Id = Guid.NewGuid(),
                ContentItemId = id,
                Channel = channel,
                VersionNo = item.CurrentVersionNo,
                Status = "Success",
                CreatedAtUtc = DateTime.UtcNow,
                ResponseJson = JsonSerializer.Serialize(new { retracted = true, reason = request.Reason })
            });
        }

        await _db.SaveChangesAsync();

        // Create admin alert for retraction
        await _alertService.CreateRetractAlertAsync(id, item.Title, request.Reason, userId);

        _logger.LogWarning("Content {ContentId} retracted by user {UserId}. Reason: {Reason}",
            id, userId, request.Reason);

        return Ok(new RetractResponse
        {
            Ok = true,
            VersionNo = item.CurrentVersionNo
        });
    }
}
