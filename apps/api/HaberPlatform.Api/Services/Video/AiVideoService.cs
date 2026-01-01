using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaberPlatform.Api.Services.Video;

/// <summary>
/// Service for managing AI video generation jobs
/// </summary>
public class AiVideoService
{
    private readonly AppDbContext _db;
    private readonly OpenAiVideoClient _videoClient;
    private readonly AiVideoPromptBuilder _promptBuilder;
    private readonly OpenAiVideoOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiVideoService> _logger;

    public AiVideoService(
        AppDbContext db,
        OpenAiVideoClient videoClient,
        AiVideoPromptBuilder promptBuilder,
        IOptions<OpenAiVideoOptions> options,
        IConfiguration configuration,
        ILogger<AiVideoService> logger)
    {
        _db = db;
        _videoClient = videoClient;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Check if AI video generation is enabled and configured
    /// </summary>
    public bool IsEnabled => _options.Enabled && _videoClient.IsConfigured;

    /// <summary>
    /// Generate AI video for content
    /// </summary>
    public async Task<AiVideoJobDto> GenerateAsync(
        Guid contentItemId,
        AiVideoGenerateRequest request,
        CancellationToken ct = default)
    {
        var contentItem = await _db.ContentItems
            .Include(c => c.Draft)
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == contentItemId, ct);

        if (contentItem == null)
        {
            throw new KeyNotFoundException($"Content item {contentItemId} not found");
        }

        // Check for existing completed video
        if (!request.Force)
        {
            var existingJob = await _db.AiVideoJobs
                .Where(j => j.ContentItemId == contentItemId && j.Status == AiVideoJobStatus.Completed)
                .OrderByDescending(j => j.CompletedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (existingJob != null)
            {
                _logger.LogInformation("Returning existing completed video job {JobId} for content {ContentId}", 
                    existingJob.Id, contentItemId);
                return ToDto(existingJob);
            }
        }

        // Check for in-progress job
        var inProgressJob = await _db.AiVideoJobs
            .Where(j => j.ContentItemId == contentItemId && 
                   (j.Status == AiVideoJobStatus.Queued || j.Status == AiVideoJobStatus.InProgress))
            .FirstOrDefaultAsync(ct);

        if (inProgressJob != null)
        {
            _logger.LogInformation("Video job {JobId} already in progress for content {ContentId}", 
                inProgressJob.Id, contentItemId);
            return ToDto(inProgressJob);
        }

        // Build prompt
        string prompt;
        if (request.Mode == AiVideoMode.CustomPrompt && !string.IsNullOrWhiteSpace(request.PromptOverride))
        {
            prompt = request.PromptOverride;
            // Add safety clauses
            if (!prompt.Contains("no real persons", StringComparison.OrdinalIgnoreCase))
            {
                prompt += " No real persons, no faces of real people, no logos, no trademarks, fictional virtual presenter only.";
            }
        }
        else
        {
            prompt = _promptBuilder.BuildPrompt(contentItem, contentItem.Draft, contentItem.Source, 
                request.Seconds, request.Size);
        }

        // Validate parameters
        var model = OpenAiVideoOptions.AllowedModels.Contains(request.Model) ? request.Model : _options.Model;
        var seconds = OpenAiVideoOptions.AllowedSeconds.Contains(request.Seconds) ? request.Seconds : _options.Seconds;
        var size = OpenAiVideoOptions.AllowedSizes.Contains(request.Size) ? request.Size : _options.Size;

        // Create job record
        var job = new AiVideoJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentItemId,
            Provider = "OpenAI",
            Model = model,
            Prompt = prompt,
            Seconds = seconds,
            Size = size,
            Status = AiVideoJobStatus.Queued,
            Progress = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.AiVideoJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created AI video job {JobId} for content {ContentId}", job.Id, contentItemId);

        // Call OpenAI API
        var response = await _videoClient.CreateVideoAsync(prompt, model, seconds, size, ct);

        if (response.Error != null || string.IsNullOrEmpty(response.Id))
        {
            job.Status = AiVideoJobStatus.Failed;
            job.Error = response.Error?.Message ?? "Failed to create video job";
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogError("AI video job {JobId} failed: {Error}", job.Id, job.Error);
            return ToDto(job);
        }

        // Update job with OpenAI ID
        job.OpenAiVideoId = response.Id;
        job.Status = AiVideoJobStatus.InProgress;
        job.Progress = response.Progress;
        job.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AI video job {JobId} submitted to OpenAI: {OpenAiId}", job.Id, response.Id);

        return ToDto(job);
    }

    /// <summary>
    /// Get job status
    /// </summary>
    public async Task<AiVideoJobDto?> GetStatusAsync(Guid contentItemId, CancellationToken ct = default)
    {
        var job = await _db.AiVideoJobs
            .Include(j => j.MediaAsset)
            .Where(j => j.ContentItemId == contentItemId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return job != null ? ToDto(job) : null;
    }

    /// <summary>
    /// Get all jobs for content
    /// </summary>
    public async Task<List<AiVideoJobDto>> GetJobsAsync(Guid contentItemId, CancellationToken ct = default)
    {
        var jobs = await _db.AiVideoJobs
            .Include(j => j.MediaAsset)
            .Where(j => j.ContentItemId == contentItemId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(ct);

        return jobs.Select(ToDto).ToList();
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    public async Task<bool> CancelAsync(Guid contentItemId, CancellationToken ct = default)
    {
        var job = await _db.AiVideoJobs
            .Where(j => j.ContentItemId == contentItemId && 
                   (j.Status == AiVideoJobStatus.Queued || j.Status == AiVideoJobStatus.InProgress))
            .FirstOrDefaultAsync(ct);

        if (job == null)
            return false;

        job.Status = AiVideoJobStatus.Cancelled;
        job.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled AI video job {JobId}", job.Id);
        return true;
    }

    /// <summary>
    /// Get prompt preview
    /// </summary>
    public async Task<AiVideoPromptPreviewResponse?> GetPromptPreviewAsync(
        Guid contentItemId,
        string? customPrompt = null,
        CancellationToken ct = default)
    {
        var contentItem = await _db.ContentItems
            .Include(c => c.Draft)
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == contentItemId, ct);

        if (contentItem == null)
            return null;

        var prompt = _promptBuilder.GetPromptPreview(
            contentItem, 
            contentItem.Draft, 
            contentItem.Source,
            customPrompt,
            _options.Seconds,
            _options.Size);

        return new AiVideoPromptPreviewResponse(
            Prompt: prompt,
            Model: _options.Model,
            Seconds: _options.Seconds,
            Size: _options.Size
        );
    }

    private AiVideoJobDto ToDto(AiVideoJob job)
    {
        string? mediaUrl = null;
        if (job.MediaAsset != null)
        {
            var basePath = _configuration["Media:PublicBasePath"] ?? "/media";
            mediaUrl = $"{basePath}/{job.MediaAsset.StoragePath}";
        }

        return new AiVideoJobDto(
            Id: job.Id,
            ContentItemId: job.ContentItemId,
            Provider: job.Provider,
            Model: job.Model,
            Prompt: job.Prompt,
            Seconds: job.Seconds,
            Size: job.Size,
            Status: job.Status,
            OpenAiVideoId: job.OpenAiVideoId,
            Progress: job.Progress,
            Error: job.Error,
            MediaUrl: mediaUrl,
            CreatedAtUtc: job.CreatedAtUtc,
            UpdatedAtUtc: job.UpdatedAtUtc,
            CompletedAtUtc: job.CompletedAtUtc
        );
    }
}

