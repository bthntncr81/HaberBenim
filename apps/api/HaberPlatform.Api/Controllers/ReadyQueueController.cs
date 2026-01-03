using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Services.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/ready-queue")]
[Authorize(Roles = "Admin,Editor")]
public class ReadyQueueController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReadyQueueController> _logger;
    private readonly ITemplateVariableResolver _variableResolver;

    public ReadyQueueController(
        AppDbContext db, 
        ILogger<ReadyQueueController> logger,
        ITemplateVariableResolver variableResolver)
    {
        _db = db;
        _logger = logger;
        _variableResolver = variableResolver;
    }

    /// <summary>
    /// Get items in the ready queue (rendered and waiting for publish approval)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReadyQueue(
        [FromQuery] string? platform = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Query content items that are ReadyToPublish with completed render jobs
        var query = _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Where(c => c.Status == "ReadyToPublish" || c.Status == "AutoReady")
            .AsQueryable();

        // Get render jobs for ready content
        var contentIds = await query.Select(c => c.Id).ToListAsync(ct);
        
        var renderJobs = await _db.RenderJobs
            .Include(r => r.Template)
            .Include(r => r.OutputMediaAsset)
            .Where(r => contentIds.Contains(r.ContentItemId) && r.Status == RenderJobStatus.Completed)
            .ToListAsync(ct);

        if (!string.IsNullOrEmpty(platform))
        {
            renderJobs = renderJobs.Where(r => r.Platform == platform).ToList();
            contentIds = renderJobs.Select(r => r.ContentItemId).Distinct().ToList();
            query = query.Where(c => contentIds.Contains(c.Id));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.IngestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = items.Select(content =>
        {
            var contentRenderJobs = renderJobs.Where(r => r.ContentItemId == content.Id).ToList();
            
            return new ReadyQueueItemDto
            {
                Id = content.Id,
                Title = content.Title,
                Summary = content.Summary,
                SourceName = content.Source?.Name ?? "Unknown",
                Category = content.Source?.Category ?? "General",
                Status = content.Status,
                CreatedAtUtc = content.IngestedAtUtc,
                UpdatedAtUtc = content.LastEditedAtUtc ?? content.IngestedAtUtc,
                PrimaryImageUrl = GetPrimaryImageUrl(content),
                RenderJobs = contentRenderJobs.Select(r => new RenderJobDto
                {
                    Id = r.Id,
                    Platform = r.Platform,
                    Format = r.Format,
                    TemplateName = r.Template?.Name ?? "Unknown",
                    Status = r.Status,
                    OutputUrl = r.OutputMediaAsset != null 
                        ? $"/media/{r.OutputMediaAsset.StoragePath}" 
                        : null,
                    CreatedAtUtc = r.CreatedAtUtc,
                    CompletedAtUtc = r.CompletedAtUtc
                }).ToList()
            };
        }).ToList();

        return Ok(new
        {
            items = result,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Get single ready queue item with all render details
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReadyQueueItem(Guid id, CancellationToken ct)
    {
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (content == null)
            return NotFound(new { error = "Content not found" });

        var renderJobs = await _db.RenderJobs
            .Include(r => r.Template)
            .Include(r => r.OutputMediaAsset)
            .Where(r => r.ContentItemId == id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(new ReadyQueueItemDto
        {
            Id = content.Id,
            Title = content.Title,
            Summary = content.Summary,
            SourceName = content.Source?.Name ?? "Unknown",
            Category = content.Source?.Category ?? "General",
            Status = content.Status,
            CreatedAtUtc = content.IngestedAtUtc,
            UpdatedAtUtc = content.LastEditedAtUtc ?? content.IngestedAtUtc,
            PrimaryImageUrl = GetPrimaryImageUrl(content),
            RenderJobs = renderJobs.Select(r => new RenderJobDto
            {
                Id = r.Id,
                Platform = r.Platform,
                Format = r.Format,
                TemplateName = r.Template?.Name ?? "Unknown",
                Status = r.Status,
                OutputUrl = r.OutputMediaAsset != null 
                    ? $"/media/{r.OutputMediaAsset.StoragePath}" 
                    : null,
                Error = r.Error,
                ResolvedTextSpecJson = r.ResolvedTextSpecJson,
                CreatedAtUtc = r.CreatedAtUtc,
                CompletedAtUtc = r.CompletedAtUtc
            }).ToList()
        });
    }

    /// <summary>
    /// Publish a ready queue item to all platforms
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishReadyQueueItem(
        Guid id,
        [FromBody] PublishReadyQueueRequest? request = null,
        CancellationToken ct = default)
    {
        var content = await _db.ContentItems
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (content == null)
            return NotFound(new { error = "Content not found" });

        if (content.Status != "ReadyToPublish" && content.Status != "AutoReady")
        {
            return BadRequest(new { error = $"Content is not in ready queue (status: {content.Status})" });
        }

        // Check if there's already a pending publish job
        var existingJob = await _db.PublishJobs
            .Where(p => p.ContentItemId == id && p.Status != "Completed" && p.Status != "Failed")
            .FirstOrDefaultAsync(ct);

        if (existingJob != null)
        {
            return Ok(new
            {
                success = true,
                message = "Publish job already exists",
                jobId = existingJob.Id,
                alreadyQueued = true
            });
        }

        // Get completed render jobs for this content
        var renderJobs = await _db.RenderJobs
            .Where(r => r.ContentItemId == id && r.Status == RenderJobStatus.Completed)
            .ToListAsync(ct);

        // Create publish job
        var publishJob = new PublishJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = id,
            Status = PublishJobStatuses.Pending,
            ScheduledAtUtc = request?.ScheduledAtUtc ?? DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.PublishJobs.Add(publishJob);

        // Update content status
        content.Status = request?.ScheduledAtUtc != null ? "Scheduled" : "Publishing";
        content.LastEditedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created publish job {JobId} for content {ContentId} with {RenderCount} renders",
            publishJob.Id, id, renderJobs.Count);

        return Ok(new
        {
            success = true,
            message = "Publish job created",
            jobId = publishJob.Id,
            renderJobCount = renderJobs.Count,
            platforms = renderJobs.Select(r => r.Platform).Distinct().ToList()
        });
    }

    /// <summary>
    /// Create render jobs for a content item using template selector
    /// </summary>
    [HttpPost("{id:guid}/render")]
    public async Task<IActionResult> CreateRenderJobs(
        Guid id,
        [FromBody] CreateRenderJobsRequest request,
        CancellationToken ct = default)
    {
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Include(c => c.Media)
            .Include(c => c.MediaLinks)
                .ThenInclude(ml => ml.MediaAsset)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (content == null)
            return NotFound(new { error = "Content not found" });

        var createdJobs = new List<RenderJobDto>();

        foreach (var platform in request.Platforms)
        {
            // Check if there's already a pending/completed render job for this platform
            var existingJob = await _db.RenderJobs
                .Where(r => r.ContentItemId == id && r.Platform == platform &&
                           (r.Status == RenderJobStatus.Queued || 
                            r.Status == RenderJobStatus.Rendering ||
                            r.Status == RenderJobStatus.Completed))
                .FirstOrDefaultAsync(ct);

            if (existingJob != null && !request.Force)
            {
                continue; // Skip this platform
            }

            // Find matching template
            var template = await FindTemplateForContentAsync(content, platform, ct);
            if (template == null)
            {
                _logger.LogWarning("No template found for content {ContentId}, platform {Platform}",
                    id, platform);
                continue;
            }

            // Resolve TextSpec if template has one
            string? resolvedTextSpecJson = null;
            if (template.Spec != null && !string.IsNullOrEmpty(template.Spec.TextSpecJson))
            {
                var published = await _db.PublishedContents
                    .FirstOrDefaultAsync(p => p.ContentItemId == id, ct);
                var vars = _variableResolver.ResolveVariables(content, published);
                resolvedTextSpecJson = _variableResolver.ResolveTextSpec(template.Spec.TextSpecJson, vars);
            }

            // Create render job
            var job = new RenderJob
            {
                Id = Guid.NewGuid(),
                ContentItemId = id,
                TemplateId = template.Id,
                Platform = platform,
                Format = template.Format,
                Status = RenderJobStatus.Queued,
                ResolvedTextSpecJson = resolvedTextSpecJson,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.RenderJobs.Add(job);

            createdJobs.Add(new RenderJobDto
            {
                Id = job.Id,
                Platform = platform,
                Format = template.Format,
                TemplateName = template.Name,
                Status = job.Status,
                CreatedAtUtc = job.CreatedAtUtc
            });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            createdJobs = createdJobs,
            message = createdJobs.Count > 0 
                ? $"Created {createdJobs.Count} render jobs" 
                : "No render jobs created (no matching templates or already exists)"
        });
    }

    /// <summary>
    /// Get current publish mode setting
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetQueueSettings(CancellationToken ct)
    {
        var publishMode = await _db.SystemSettings
            .Where(s => s.Key == "PUBLISH_MODE")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct) ?? "Approved";

        return Ok(new
        {
            publishMode,
            modes = new[] { "Auto", "Approved" },
            description = publishMode == "Auto" 
                ? "Content is published automatically after rendering"
                : "Content goes to ready queue for manual publish approval"
        });
    }

    /// <summary>
    /// Update publish mode setting
    /// </summary>
    [HttpPut("settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateQueueSettings(
        [FromBody] UpdateQueueSettingsRequest request,
        CancellationToken ct)
    {
        if (request.PublishMode != "Auto" && request.PublishMode != "Approved")
        {
            return BadRequest(new { error = "Invalid publish mode. Must be 'Auto' or 'Approved'" });
        }

        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "PUBLISH_MODE", ct);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = "PUBLISH_MODE",
                Value = request.PublishMode,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = request.PublishMode;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated PUBLISH_MODE to {Mode}", request.PublishMode);

        return Ok(new { success = true, publishMode = request.PublishMode });
    }

    private async Task<PublishTemplate?> FindTemplateForContentAsync(
        ContentItem content,
        string platform,
        CancellationToken ct)
    {
        // Get assignments for this source and platform
        var assignments = await _db.SourceTemplateAssignments
            .Include(a => a.Template)
            .Where(a => a.SourceId == content.SourceId && 
                       a.Platform == platform && 
                       a.IsActive)
            .OrderByDescending(a => a.PriorityOverride ?? a.Template!.Priority)
            .ToListAsync(ct);

        // Return first matching template
        foreach (var assignment in assignments)
        {
            if (assignment.Template?.IsActive == true)
            {
                return assignment.Template;
            }
        }

        // Fallback: find any active template for this platform
        return await _db.PublishTemplates
            .Where(t => t.Platform == platform && t.IsActive)
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync(ct);
    }

    private string? GetPrimaryImageUrl(ContentItem content)
    {
        // This is a simplified version - in real implementation, 
        // you'd query ContentMediaLinks
        return null;
    }
}

// DTOs
public class ReadyQueueItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Summary { get; set; }
    public string SourceName { get; set; } = "";
    public string? Category { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public List<RenderJobDto> RenderJobs { get; set; } = new();
}

public class RenderJobDto
{
    public Guid Id { get; set; }
    public string Platform { get; set; } = "";
    public string Format { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? OutputUrl { get; set; }
    public string? Error { get; set; }
    public string? ResolvedTextSpecJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class PublishReadyQueueRequest
{
    public int Priority { get; set; } = 100;
    public DateTime? ScheduledAtUtc { get; set; }
}

public class CreateRenderJobsRequest
{
    public List<string> Platforms { get; set; } = new();
    public bool Force { get; set; } = false;
}

public class UpdateQueueSettingsRequest
{
    public string PublishMode { get; set; } = "Approved";
}

