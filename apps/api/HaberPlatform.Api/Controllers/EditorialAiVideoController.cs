using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Video;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// AI Video generation endpoints for editorial content
/// </summary>
[ApiController]
[Route("api/v1/editorial/items/{id}/ai-video")]
[Authorize(Roles = "Admin,Editor")]
public class EditorialAiVideoController : ControllerBase
{
    private readonly AiVideoService _videoService;
    private readonly ILogger<EditorialAiVideoController> _logger;

    public EditorialAiVideoController(
        AiVideoService videoService,
        ILogger<EditorialAiVideoController> logger)
    {
        _videoService = videoService;
        _logger = logger;
    }

    /// <summary>
    /// Generate AI video for content
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid id, [FromBody] AiVideoGenerateRequest? request = null)
    {
        request ??= new AiVideoGenerateRequest();
        
        if (!_videoService.IsEnabled)
        {
            return BadRequest(new 
            { 
                success = false, 
                message = "AI video generation is not enabled. Please configure OPENAI_API_KEY.",
                enabled = false
            });
        }

        try
        {
            var job = await _videoService.GenerateAsync(id, request);
            
            return Ok(new
            {
                success = true,
                message = job.Status switch
                {
                    "Queued" or "InProgress" => "Video generation started",
                    "Completed" => "Video already exists",
                    "Failed" => "Video generation failed",
                    _ => "Video job created"
                },
                job
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI video for content {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Get AI video job status for content
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var job = await _videoService.GetStatusAsync(id);
        
        if (job == null)
        {
            return Ok(new 
            { 
                success = true, 
                hasVideo = false,
                message = "No video job found for this content"
            });
        }

        return Ok(new
        {
            success = true,
            hasVideo = job.Status == "Completed",
            job
        });
    }

    /// <summary>
    /// Get all AI video jobs for content
    /// </summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs(Guid id)
    {
        var jobs = await _videoService.GetJobsAsync(id);
        
        return Ok(new
        {
            success = true,
            count = jobs.Count,
            jobs
        });
    }

    /// <summary>
    /// Cancel in-progress video job
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var cancelled = await _videoService.CancelAsync(id);
        
        if (!cancelled)
        {
            return NotFound(new { success = false, message = "No active video job to cancel" });
        }

        return Ok(new { success = true, message = "Video job cancelled" });
    }

    /// <summary>
    /// Get prompt preview for content
    /// </summary>
    [HttpGet("prompt")]
    public async Task<IActionResult> GetPromptPreview(Guid id, [FromQuery] string? customPrompt = null)
    {
        var preview = await _videoService.GetPromptPreviewAsync(id, customPrompt);
        
        if (preview == null)
        {
            return NotFound(new { success = false, message = "Content not found" });
        }

        return Ok(new
        {
            success = true,
            preview
        });
    }

    /// <summary>
    /// Get configuration status
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            enabled = _videoService.IsEnabled,
            configured = _videoService.IsEnabled,
            allowedModels = OpenAiVideoOptions.AllowedModels,
            allowedSeconds = OpenAiVideoOptions.AllowedSeconds,
            allowedSizes = OpenAiVideoOptions.AllowedSizes
        });
    }
}

