using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services.Video;

/// <summary>
/// Startup service that checks if FFmpeg is available and creates an admin alert if not.
/// </summary>
public class FfmpegStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FfmpegStartupCheck> _logger;

    public FfmpegStartupCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<FfmpegStartupCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var videoRenderer = scope.ServiceProvider.GetRequiredService<IVideoRenderer>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var isAvailable = await videoRenderer.CheckFfmpegAvailableAsync(cancellationToken);

        if (!isAvailable)
        {
            _logger.LogWarning("FFmpeg is not available. Video rendering will not work.");

            // Check if we already have an active alert for this
            var existingAlert = await db.AdminAlerts
                .Where(a => a.Type == "FfmpegMissing" && !a.IsResolved)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingAlert == null)
            {
                // Create admin alert
                var alert = new AdminAlert
                {
                    Id = Guid.NewGuid(),
                    Type = "FfmpegMissing",
                    Severity = "Warning",
                    Title = "FFmpeg Not Available",
                    Message = "FFmpeg executable was not found. Video rendering features will not work. " +
                             "Please install FFmpeg and ensure it's in the system PATH, or configure " +
                             "VideoRender:FfmpegPath in appsettings.json.",
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.AdminAlerts.Add(alert);
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Created admin alert for missing FFmpeg");
            }
        }
        else
        {
            _logger.LogInformation("FFmpeg is available and ready for video rendering");

            // Resolve any existing alerts
            var existingAlerts = await db.AdminAlerts
                .Where(a => a.Type == "FfmpegMissing" && !a.IsResolved)
                .ToListAsync(cancellationToken);

            foreach (var alert in existingAlerts)
            {
                alert.IsResolved = true;
                alert.ResolvedAtUtc = DateTime.UtcNow;
            }

            if (existingAlerts.Any())
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Resolved {Count} FFmpeg missing alerts", existingAlerts.Count);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

