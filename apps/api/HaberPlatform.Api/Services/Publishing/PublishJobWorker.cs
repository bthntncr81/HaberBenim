using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Background worker that processes pending publish jobs with concurrency safety
/// </summary>
public class PublishJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PublishJobWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private const int MaxJobsPerBatch = 10;
    private const int MaxRetryAttempts = 5;

    public PublishJobWorker(IServiceScopeFactory scopeFactory, ILogger<PublishJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PublishJobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PublishJobWorker");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _logger.LogInformation("PublishJobWorker stopped");
    }

    public async Task<int> ProcessDueJobsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Use transaction with row-level locking to safely claim jobs
        // This prevents multiple workers from picking the same job
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // Find and lock due jobs using FOR UPDATE SKIP LOCKED
            // This is PostgreSQL-specific and provides safe concurrency
            var jobIds = await db.Database
                .SqlQuery<Guid>($@"
                    SELECT ""Id"" 
                    FROM ""PublishJobs"" 
                    WHERE ""Status"" = 'Pending' 
                      AND ""ScheduledAtUtc"" <= {now}
                      AND (""NextRetryAtUtc"" IS NULL OR ""NextRetryAtUtc"" <= {now})
                    ORDER BY ""ScheduledAtUtc""
                    LIMIT {MaxJobsPerBatch}
                    FOR UPDATE SKIP LOCKED")
                .ToListAsync(ct);

            if (jobIds.Count == 0)
            {
                await transaction.CommitAsync(ct);
                return 0;
            }

            // Mark all selected jobs as Running atomically
            await db.PublishJobs
                .Where(j => jobIds.Contains(j.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, PublishJobStatuses.Running)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                    .SetProperty(j => j.LastAttemptAtUtc, now), ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Claimed {Count} publish jobs for processing", jobIds.Count);

            // Process jobs outside the transaction
            var processed = 0;
            foreach (var jobId in jobIds)
            {
                if (ct.IsCancellationRequested) break;

                await ProcessJobAsync(scope.ServiceProvider, db, jobId, ct);
                processed++;
            }

            return processed;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Error claiming publish jobs");
            throw;
        }
    }

    private async Task ProcessJobAsync(
        IServiceProvider services,
        AppDbContext db,
        Guid jobId,
        CancellationToken ct)
    {
        // Reload job from DB
        var job = await db.PublishJobs.FindAsync([jobId], ct);
        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found after claiming", jobId);
            return;
        }

        // Double-check status (another worker might have processed it)
        if (job.Status != PublishJobStatuses.Running)
        {
            _logger.LogInformation("Job {JobId} status changed to {Status}, skipping", jobId, job.Status);
            return;
        }

        try
        {
            // Get orchestrator
            var orchestrator = services.GetRequiredService<PublisherOrchestrator>();

            // Execute publish with version number
            var result = await orchestrator.PublishAsync(job.ContentItemId, job.VersionNo, job.CreatedByUserId, ct);

            if (result.AllSucceeded)
            {
                job.Status = PublishJobStatuses.Succeeded;
                job.LastError = null;
                _logger.LogInformation("Publish job {JobId} succeeded for content {ContentId} v{Version}", 
                    job.Id, job.ContentItemId, job.VersionNo);
            }
            else
            {
                HandleJobFailure(job, result.Error ?? "Some channels failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publish job {JobId} failed with exception", job.Id);
            HandleJobFailure(job, ex.Message);
        }

        await db.SaveChangesAsync(ct);
    }

    private void HandleJobFailure(PublishJob job, string error)
    {
        job.LastError = error.Length > 2000 ? error[..2000] : error;

        if (job.AttemptCount >= MaxRetryAttempts)
        {
            // Max retries reached, mark as failed permanently
            job.Status = PublishJobStatuses.Failed;
            _logger.LogWarning("Publish job {JobId} permanently failed after {Attempts} attempts", 
                job.Id, job.AttemptCount);
        }
        else
        {
            // Schedule retry with exponential backoff
            job.Status = PublishJobStatuses.Pending;
            job.NextRetryAtUtc = CalculateNextRetry(job.AttemptCount);
            _logger.LogInformation("Publish job {JobId} will retry at {RetryTime}", 
                job.Id, job.NextRetryAtUtc);
        }
    }

    private static DateTime CalculateNextRetry(int attemptCount)
    {
        // Exponential backoff: 1 min, 5 min, 15 min, 60 min
        var delays = new[] { 1, 5, 15, 60, 60 };
        var index = Math.Min(attemptCount - 1, delays.Length - 1);
        return DateTime.UtcNow.AddMinutes(delays[index]);
    }
}
