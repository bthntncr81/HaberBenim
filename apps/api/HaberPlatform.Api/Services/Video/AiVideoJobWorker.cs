using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Video;

/// <summary>
/// Background worker that processes AI video generation jobs
/// </summary>
public class AiVideoJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiVideoJobWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public AiVideoJobWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AiVideoJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiVideoJobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AiVideoJobWorker");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("AiVideoJobWorker stopped");
    }

    private async Task ProcessJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var videoClient = scope.ServiceProvider.GetRequiredService<OpenAiVideoClient>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<OpenAiVideoOptions>>().Value;
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return; // AI video not configured
        }

        // Get jobs that need processing
        var jobs = await db.AiVideoJobs
            .Where(j => j.Status == AiVideoJobStatus.InProgress && 
                        j.OpenAiVideoId != null)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        foreach (var job in jobs)
        {
            try
            {
                await ProcessJobAsync(db, videoClient, configuration, job, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI video job {JobId}", job.Id);
                
                job.Status = AiVideoJobStatus.Failed;
                job.Error = ex.Message;
                job.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task ProcessJobAsync(
        AppDbContext db, 
        OpenAiVideoClient videoClient, 
        IConfiguration configuration,
        AiVideoJob job, 
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.OpenAiVideoId))
        {
            _logger.LogWarning("Job {JobId} has no OpenAI video ID", job.Id);
            return;
        }

        // Check status with OpenAI
        var status = await videoClient.RetrieveVideoAsync(job.OpenAiVideoId, ct);

        job.Progress = status.Progress;
        job.UpdatedAtUtc = DateTime.UtcNow;

        if (status.Status == "completed")
        {
            await HandleCompletedJobAsync(db, videoClient, configuration, job, ct);
        }
        else if (status.Status == "failed")
        {
            job.Status = AiVideoJobStatus.Failed;
            job.Error = status.FailedReason ?? status.Error?.Message ?? "Video generation failed";
            _logger.LogWarning("AI video job {JobId} failed: {Error}", job.Id, job.Error);
        }
        else
        {
            // Still in progress
            _logger.LogDebug("AI video job {JobId} in progress: {Progress}%", job.Id, status.Progress);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task HandleCompletedJobAsync(
        AppDbContext db,
        OpenAiVideoClient videoClient,
        IConfiguration configuration,
        AiVideoJob job,
        CancellationToken ct)
    {
        _logger.LogInformation("AI video job {JobId} completed, downloading video", job.Id);

        // Download video content
        var videoBytes = await videoClient.DownloadVideoContentAsync(job.OpenAiVideoId!, ct);

        if (videoBytes == null || videoBytes.Length == 0)
        {
            job.Status = AiVideoJobStatus.Failed;
            job.Error = "Failed to download video content";
            return;
        }

        // Save to storage
        var mediaRootDir = configuration["Media:RootDir"] ?? "tools/storage/media";
        var assetId = Guid.NewGuid();
        var fileName = $"{assetId}.mp4";
        var filePath = Path.Combine(mediaRootDir, fileName);

        // Ensure directory exists
        Directory.CreateDirectory(mediaRootDir);

        await File.WriteAllBytesAsync(filePath, videoBytes, ct);

        _logger.LogInformation("Saved AI video to {FilePath}, size: {Size} bytes", filePath, videoBytes.Length);

        // Create MediaAsset
        var mediaAsset = new MediaAsset
        {
            Id = assetId,
            Kind = "Video",
            Origin = "AI",
            StoragePath = fileName,
            ContentType = "video/mp4",
            SizeBytes = videoBytes.Length,
            Width = ParseWidth(job.Size),
            Height = ParseHeight(job.Size),
            AltText = $"AI generated video for content",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.MediaAssets.Add(mediaAsset);

        // Link to content
        var link = new ContentMediaLink
        {
            Id = Guid.NewGuid(),
            ContentItemId = job.ContentItemId,
            MediaAssetId = assetId,
            IsPrimary = false, // Videos are not primary images
            SortOrder = 100, // After images
            CreatedAtUtc = DateTime.UtcNow
        };

        db.ContentMediaLinks.Add(link);

        // Update job
        job.Status = AiVideoJobStatus.Completed;
        job.MediaAssetId = assetId;
        job.CompletedAtUtc = DateTime.UtcNow;
        job.Progress = 100;

        _logger.LogInformation("AI video job {JobId} completed successfully, asset: {AssetId}", job.Id, assetId);
    }

    private static int ParseWidth(string size)
    {
        var parts = size.Split('x');
        return parts.Length > 0 && int.TryParse(parts[0], out var w) ? w : 1280;
    }

    private static int ParseHeight(string size)
    {
        var parts = size.Split('x');
        return parts.Length > 1 && int.TryParse(parts[1], out var h) ? h : 720;
    }
}

