using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Publishing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/emergency-queue")]
[Authorize(Roles = "Admin,Editor")]
public class EmergencyQueueController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmergencyDetector _emergencyDetector;
    private readonly IPublishScheduler _scheduler;
    private readonly IPublishService _publishService;
    private readonly ILogger<EmergencyQueueController> _logger;

    public EmergencyQueueController(
        AppDbContext db,
        IEmergencyDetector emergencyDetector,
        IPublishScheduler scheduler,
        IPublishService publishService,
        ILogger<EmergencyQueueController> logger)
    {
        _db = db;
        _emergencyDetector = emergencyDetector;
        _scheduler = scheduler;
        _publishService = publishService;
        _logger = logger;
    }

    /// <summary>
    /// Get all items in the emergency queue
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.EmergencyQueueItems
            .Include(e => e.ContentItem)
            .ThenInclude(c => c!.Source)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }
        else
        {
            // Default: show pending items
            query = query.Where(e => e.Status == EmergencyQueueStatus.Pending);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.DetectedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmergencyQueueItemDto
            {
                Id = e.Id,
                ContentItemId = e.ContentItemId,
                Title = e.ContentItem!.Title,
                Category = e.ContentItem.Source!.Category,
                SourceName = e.ContentItem.Source.Name,
                Priority = e.Priority,
                Status = e.Status,
                MatchedKeywords = e.MatchedKeywordsCsv != null 
                    ? e.MatchedKeywordsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() 
                    : new List<string>(),
                DetectedAtUtc = e.DetectedAtUtc,
                PublishedAtUtc = e.PublishedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new
        {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    /// <summary>
    /// Get a single emergency queue item
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
    {
        var item = await _db.EmergencyQueueItems
            .Include(e => e.ContentItem)
            .ThenInclude(c => c!.Source)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (item == null)
            return NotFound(new { error = "Emergency queue item not found" });

        return Ok(new EmergencyQueueItemDto
        {
            Id = item.Id,
            ContentItemId = item.ContentItemId,
            Title = item.ContentItem!.Title,
            Category = item.ContentItem.Source!.Category,
            SourceName = item.ContentItem.Source.Name,
            Priority = item.Priority,
            Status = item.Status,
            MatchedKeywords = item.MatchedKeywords,
            DetectedAtUtc = item.DetectedAtUtc,
            PublishedAtUtc = item.PublishedAtUtc
        });
    }

    /// <summary>
    /// Manually add content to emergency queue
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddToQueue(
        [FromBody] AddToEmergencyQueueRequest request,
        CancellationToken ct)
    {
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == request.ContentItemId, ct);

        if (content == null)
            return NotFound(new { error = "Content not found" });

        // Auto-detect or use manual priority
        EmergencyDetectionResult detection;
        if (request.AutoDetect)
        {
            detection = await _emergencyDetector.DetectEmergencyAsync(content, ct);
            if (!detection.IsEmergency)
            {
                detection = EmergencyDetectionResult.Detected(
                    request.Priority ?? 50,
                    new List<string>(),
                    "Manual override");
            }
        }
        else
        {
            detection = EmergencyDetectionResult.Detected(
                request.Priority ?? 50,
                new List<string>(),
                request.Reason ?? "Manual addition");
        }

        var item = await _emergencyDetector.EnqueueEmergencyAsync(
            request.ContentItemId,
            detection,
            ct);

        // Update target platforms if specified
        if (request.Platforms?.Count > 0)
        {
            item.TargetPlatformsCsv = string.Join(",", request.Platforms);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new
        {
            success = true,
            item = new EmergencyQueueItemDto
            {
                Id = item.Id,
                ContentItemId = item.ContentItemId,
                Title = content.Title,
                Category = content.Source?.Category,
                SourceName = content.Source?.Name ?? "",
                Priority = item.Priority,
                Status = item.Status,
                MatchedKeywords = item.MatchedKeywords,
                DetectedAtUtc = item.DetectedAtUtc
            }
        });
    }

    /// <summary>
    /// Publish emergency content immediately
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishEmergency(Guid id, CancellationToken ct)
    {
        var item = await _db.EmergencyQueueItems
            .Include(e => e.ContentItem)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (item == null)
            return NotFound(new { error = "Emergency queue item not found" });

        if (item.Status != EmergencyQueueStatus.Pending)
            return BadRequest(new { error = $"Item is not pending (status: {item.Status})" });

        // Update status
        item.Status = EmergencyQueueStatus.Publishing;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Create publish job with emergency priority
            var content = item.ContentItem!;
            var publishJobId = await _publishService.CreateEmergencyPublishJobAsync(
                content.Id,
                content.CurrentVersionNo,
                item.TargetPlatforms,
                ct);

            // Update status
            item.Status = EmergencyQueueStatus.Published;
            item.PublishedAtUtc = DateTime.UtcNow;
            item.ProcessedByUserId = GetCurrentUserId();
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Emergency content {ContentId} published via queue item {QueueItemId}",
                content.Id, id);

            return Ok(new
            {
                success = true,
                publishJobId,
                message = "Emergency content queued for immediate publishing"
            });
        }
        catch (Exception ex)
        {
            // Revert status on failure
            item.Status = EmergencyQueueStatus.Pending;
            await _db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Failed to publish emergency content {ContentId}", item.ContentItemId);
            return StatusCode(500, new { error = "Failed to publish emergency content" });
        }
    }

    /// <summary>
    /// Cancel an emergency queue item
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelEmergency(Guid id, CancellationToken ct)
    {
        var item = await _db.EmergencyQueueItems.FindAsync(new object[] { id }, ct);

        if (item == null)
            return NotFound(new { error = "Emergency queue item not found" });

        if (item.Status != EmergencyQueueStatus.Pending)
            return BadRequest(new { error = $"Item is not pending (status: {item.Status})" });

        item.Status = EmergencyQueueStatus.Cancelled;
        item.CancelledAtUtc = DateTime.UtcNow;
        item.ProcessedByUserId = GetCurrentUserId();
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Emergency item cancelled" });
    }

    /// <summary>
    /// Update priority of an emergency queue item
    /// </summary>
    [HttpPut("{id:guid}/priority")]
    public async Task<IActionResult> UpdatePriority(
        Guid id,
        [FromBody] UpdatePriorityRequest request,
        CancellationToken ct)
    {
        var item = await _db.EmergencyQueueItems.FindAsync(new object[] { id }, ct);

        if (item == null)
            return NotFound(new { error = "Emergency queue item not found" });

        if (item.Status != EmergencyQueueStatus.Pending)
            return BadRequest(new { error = "Can only update priority of pending items" });

        item.Priority = Math.Clamp(request.Priority, 1, 100);
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, priority = item.Priority });
    }

    /// <summary>
    /// Detect if content qualifies as emergency
    /// </summary>
    [HttpPost("detect")]
    public async Task<IActionResult> DetectEmergency(
        [FromBody] DetectEmergencyRequest request,
        CancellationToken ct)
    {
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == request.ContentItemId, ct);

        if (content == null)
            return NotFound(new { error = "Content not found" });

        var detection = await _emergencyDetector.DetectEmergencyAsync(content, ct);

        return Ok(new
        {
            contentId = content.Id,
            title = content.Title,
            detection.IsEmergency,
            detection.Priority,
            detection.MatchedKeywords,
            detection.Reason,
            detection.IsBreakingNews,
            detection.CategoryMatch,
            detection.TrustedSource
        });
    }

    /// <summary>
    /// Get emergency queue statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var pending = await _db.EmergencyQueueItems
            .CountAsync(e => e.Status == EmergencyQueueStatus.Pending, ct);

        var publishedToday = await _db.EmergencyQueueItems
            .CountAsync(e => e.Status == EmergencyQueueStatus.Published && 
                            e.PublishedAtUtc >= DateTime.UtcNow.Date, ct);

        var cancelledToday = await _db.EmergencyQueueItems
            .CountAsync(e => e.Status == EmergencyQueueStatus.Cancelled && 
                            e.CancelledAtUtc >= DateTime.UtcNow.Date, ct);

        var avgPriority = await _db.EmergencyQueueItems
            .Where(e => e.Status == EmergencyQueueStatus.Pending)
            .AverageAsync(e => (double?)e.Priority, ct) ?? 0;

        return Ok(new
        {
            pending,
            publishedToday,
            cancelledToday,
            averagePriority = Math.Round(avgPriority, 1)
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class AddToEmergencyQueueRequest
{
    public Guid ContentItemId { get; set; }
    public int? Priority { get; set; }
    public string? Reason { get; set; }
    public bool AutoDetect { get; set; } = true;
    public List<string>? Platforms { get; set; }
}

public class UpdatePriorityRequest
{
    public int Priority { get; set; }
}

public class DetectEmergencyRequest
{
    public Guid ContentItemId { get; set; }
}

