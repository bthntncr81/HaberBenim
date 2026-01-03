using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Services.Video;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services.Templates;

public class RenderJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RenderJobWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public RenderJobWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RenderJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RenderJobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RenderJobWorker");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("RenderJobWorker stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var imageRenderService = scope.ServiceProvider.GetRequiredService<ITemplateRenderService>();
        var videoRenderer = scope.ServiceProvider.GetRequiredService<IVideoRenderer>();

        // Get queued jobs (both image and video)
        var jobs = await db.RenderJobs
            .Where(j => j.Status == RenderJobStatus.Queued)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(5)
            .ToListAsync(ct);

        foreach (var job in jobs)
        {
            try
            {
                _logger.LogInformation("Processing render job {JobId} (Type: {OutputType})", job.Id, job.OutputType);
                
                bool success;
                string? error;

                if (job.OutputType == RenderOutputTypes.Video)
                {
                    // Video rendering using FFmpeg
                    var result = await videoRenderer.RenderVideoAsync(job, ct);
                    success = result.Success;
                    error = result.Error;
                }
                else
                {
                    // Image rendering using ImageSharp
                    var result = await imageRenderService.ProcessRenderJobAsync(job, ct);
                    success = result.Success;
                    error = result.Error;
                }

                if (success)
                {
                    _logger.LogInformation("Render job {JobId} completed successfully", job.Id);
                    
                    // Check publish mode and handle accordingly
                    await HandlePostRenderAsync(db, job, ct);
                }
                else
                {
                    _logger.LogWarning("Render job {JobId} failed: {Error}", job.Id, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process render job {JobId}", job.Id);
                
                job.Status = RenderJobStatus.Failed;
                job.Error = ex.Message;
                job.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task HandlePostRenderAsync(AppDbContext db, RenderJob job, CancellationToken ct)
    {
        // Get publish mode from settings
        var publishMode = await db.SystemSettings
            .Where(s => s.Key == "PUBLISH_MODE")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct) ?? "Approved";

        if (publishMode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            // Auto mode: Create PublishJob immediately
            var existingJob = await db.PublishJobs
                .AnyAsync(p => p.ContentItemId == job.ContentItemId && 
                              p.Status != "Completed" && 
                              p.Status != "Failed", ct);

            if (!existingJob)
            {
                var publishJob = new PublishJob
                {
                    Id = Guid.NewGuid(),
                    ContentItemId = job.ContentItemId,
                    Status = PublishJobStatuses.Pending,
                    ScheduledAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.PublishJobs.Add(publishJob);
                await db.SaveChangesAsync(ct);

                _logger.LogInformation("Auto-created publish job {PublishJobId} for content {ContentId}",
                    publishJob.Id, job.ContentItemId);
            }
        }
        else
        {
            // Approved mode: Content goes to ReadyQueue
            // Update content status to ReadyToPublish if not already
            var content = await db.ContentItems.FindAsync(new object[] { job.ContentItemId }, ct);
            if (content != null && content.Status != "Published" && content.Status != "ReadyToPublish")
            {
                content.Status = "ReadyToPublish";
                content.LastEditedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                _logger.LogInformation("Content {ContentId} moved to ReadyQueue after render", job.ContentItemId);
            }
        }
    }
}

