using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Services.Templates;
using HaberPlatform.Api.Services.Video;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/editorial/items/{id:guid}/render-video")]
[Authorize(Roles = "Admin,Editor")]
public class VideoRenderController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IVideoRenderer _videoRenderer;
    private readonly ITemplateSelector _templateSelector;
    private readonly ITemplateVariableResolver _resolver;
    private readonly ILogger<VideoRenderController> _logger;

    public VideoRenderController(
        AppDbContext db,
        IVideoRenderer videoRenderer,
        ITemplateSelector templateSelector,
        ITemplateVariableResolver resolver,
        ILogger<VideoRenderController> logger)
    {
        _db = db;
        _videoRenderer = videoRenderer;
        _templateSelector = templateSelector;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Create a video render job for a content item
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RenderVideo(
        Guid id,
        [FromQuery] string platform = "Instagram",
        [FromBody] VideoRenderRequest? request = null,
        CancellationToken ct = default)
    {
        // Validate platform
        var validPlatforms = new[] { "Instagram", "YouTube", "TikTok" };
        if (!validPlatforms.Contains(platform))
        {
            return BadRequest(new { error = $"Invalid platform. Must be one of: {string.Join(", ", validPlatforms)}" });
        }

        // Check FFmpeg availability
        var ffmpegAvailable = await _videoRenderer.CheckFfmpegAvailableAsync(ct);
        if (!ffmpegAvailable)
        {
            return BadRequest(new { error = "FFmpeg is not available. Video rendering is disabled." });
        }

        // Load content
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (content == null)
            return NotFound(new { error = "Content not found" });

        // Find source video
        var sourceVideo = await _db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == id && l.MediaAsset.Kind == MediaKinds.Video)
            .OrderByDescending(l => l.IsPrimary)
            .FirstOrDefaultAsync(ct);

        if (sourceVideo?.MediaAsset == null && request?.SourceVideoAssetId == null)
        {
            return BadRequest(new { error = "No source video found for this content. Provide sourceVideoAssetId or ensure content has a video asset." });
        }

        var sourceVideoAssetId = request?.SourceVideoAssetId ?? sourceVideo?.MediaAsset.Id;

        // Check for existing job
        if (!request?.Force ?? true)
        {
            var existingJob = await _db.RenderJobs
                .Where(j => j.ContentItemId == id && 
                           j.Platform == platform && 
                           j.OutputType == RenderOutputTypes.Video &&
                           (j.Status == RenderJobStatus.Queued || 
                            j.Status == RenderJobStatus.Rendering ||
                            j.Status == RenderJobStatus.Completed))
                .FirstOrDefaultAsync(ct);

            if (existingJob != null)
            {
                return Ok(new
                {
                    success = true,
                    message = existingJob.Status == RenderJobStatus.Completed 
                        ? "Video already rendered" 
                        : "Video render job already exists",
                    job = MapJobToDto(existingJob),
                    alreadyExists = true
                });
            }
        }

        // Find template for video format
        var videoFormat = VideoFormats.GetFormatForPlatform(platform);
        var template = await FindVideoTemplateAsync(content, platform, videoFormat, ct);
        
        if (template == null)
        {
            return BadRequest(new { error = $"No video template found for {platform} ({videoFormat})" });
        }

        // Resolve text spec if template has one
        string? resolvedTextSpecJson = null;
        if (template.Spec?.TextSpecJson != null)
        {
            var published = await _db.PublishedContents
                .FirstOrDefaultAsync(p => p.ContentItemId == id, ct);
            var vars = _resolver.ResolveVariables(content, published);
            resolvedTextSpecJson = _resolver.ResolveTextSpec(template.Spec.TextSpecJson, vars);
        }

        // Create render job
        var job = new RenderJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = id,
            TemplateId = template.Id,
            Platform = platform,
            Format = videoFormat,
            OutputType = RenderOutputTypes.Video,
            SourceVideoAssetId = sourceVideoAssetId,
            Status = RenderJobStatus.Queued,
            ResolvedTextSpecJson = resolvedTextSpecJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.RenderJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created video render job {JobId} for content {ContentId}, platform {Platform}",
            job.Id, id, platform);

        return Ok(new
        {
            success = true,
            message = "Video render job created",
            job = MapJobToDto(job)
        });
    }

    /// <summary>
    /// Get video render status for a content item
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus(Guid id, [FromQuery] string? platform = null, CancellationToken ct = default)
    {
        var query = _db.RenderJobs
            .Include(j => j.Template)
            .Include(j => j.OutputMediaAsset)
            .Where(j => j.ContentItemId == id && j.OutputType == RenderOutputTypes.Video);

        if (!string.IsNullOrEmpty(platform))
        {
            query = query.Where(j => j.Platform == platform);
        }

        var jobs = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new
        {
            contentId = id,
            videoJobs = jobs.Select(MapJobToDto).ToList()
        });
    }

    /// <summary>
    /// Process a queued video render job immediately (for testing)
    /// </summary>
    [HttpPost("{jobId:guid}/process")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ProcessJob(Guid id, Guid jobId, CancellationToken ct)
    {
        var job = await _db.RenderJobs
            .FirstOrDefaultAsync(j => j.Id == jobId && j.ContentItemId == id, ct);

        if (job == null)
            return NotFound(new { error = "Job not found" });

        if (job.Status != RenderJobStatus.Queued)
            return BadRequest(new { error = $"Job is not queued (status: {job.Status})" });

        var result = await _videoRenderer.RenderVideoAsync(job, ct);

        return Ok(new
        {
            success = result.Success,
            error = result.Error,
            outputUrl = result.OutputUrl,
            durationSeconds = result.DurationSeconds
        });
    }

    /// <summary>
    /// Check if FFmpeg is available
    /// </summary>
    [HttpGet("check-ffmpeg")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckFfmpeg(CancellationToken ct)
    {
        var available = await _videoRenderer.CheckFfmpegAvailableAsync(ct);
        return Ok(new
        {
            ffmpegAvailable = available,
            message = available 
                ? "FFmpeg is available" 
                : "FFmpeg is not available. Install FFmpeg or configure VideoRender:FfmpegPath"
        });
    }

    private async Task<PublishTemplate?> FindVideoTemplateAsync(
        ContentItem content,
        string platform,
        string format,
        CancellationToken ct)
    {
        // First try to find via source template assignments
        var assignment = await _db.SourceTemplateAssignments
            .Include(a => a.Template)
            .ThenInclude(t => t!.Spec)
            .Where(a => a.SourceId == content.SourceId && 
                       a.Platform == platform && 
                       a.IsActive &&
                       a.Template!.Format == format &&
                       a.Template.IsActive)
            .OrderByDescending(a => a.PriorityOverride ?? a.Template!.Priority)
            .FirstOrDefaultAsync(ct);

        if (assignment?.Template != null)
            return assignment.Template;

        // Fallback: find any active video template for this platform/format
        return await _db.PublishTemplates
            .Include(t => t.Spec)
            .Where(t => t.Platform == platform && 
                       t.Format == format && 
                       t.IsActive)
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync(ct);
    }

    private static VideoRenderJobDto MapJobToDto(RenderJob job)
    {
        return new VideoRenderJobDto
        {
            Id = job.Id,
            ContentItemId = job.ContentItemId,
            Platform = job.Platform,
            Format = job.Format,
            TemplateName = job.Template?.Name,
            Status = job.Status,
            Progress = job.Progress,
            OutputType = job.OutputType,
            OutputUrl = job.OutputMediaAsset != null 
                ? $"/media/{job.OutputMediaAsset.StoragePath}" 
                : null,
            Error = job.Error,
            CreatedAtUtc = job.CreatedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc
        };
    }
}

public class VideoRenderRequest
{
    public Guid? SourceVideoAssetId { get; set; }
    public bool Force { get; set; } = false;
}

public class VideoRenderJobDto
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    public string Platform { get; set; } = "";
    public string Format { get; set; } = "";
    public string? TemplateName { get; set; }
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string OutputType { get; set; } = "";
    public string? OutputUrl { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

