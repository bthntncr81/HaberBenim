using System.Text.Json;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services.Publishing;

public interface IPublishingPolicyService
{
    /// <summary>
    /// Get the current publishing policy
    /// </summary>
    Task<PublishingPolicy> GetPolicyAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Update the publishing policy
    /// </summary>
    Task<PublishingPolicy> UpdatePolicyAsync(PublishingPolicy policy, CancellationToken ct = default);
    
    /// <summary>
    /// Get daily publishing stats for a platform
    /// </summary>
    Task<DailyPublishingStats> GetDailyStatsAsync(string platform, CancellationToken ct = default);
    
    /// <summary>
    /// Get stats for all platforms
    /// </summary>
    Task<List<DailyPublishingStats>> GetAllDailyStatsAsync(CancellationToken ct = default);
}

public class PublishingPolicyService : IPublishingPolicyService
{
    private const string PolicyKey = "PUBLISHING_POLICY";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppDbContext _db;
    private readonly ILogger<PublishingPolicyService> _logger;

    public PublishingPolicyService(AppDbContext db, ILogger<PublishingPolicyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PublishingPolicy> GetPolicyAsync(CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == PolicyKey, ct);

        if (setting == null || string.IsNullOrEmpty(setting.Value))
        {
            return new PublishingPolicy();
        }

        try
        {
            return JsonSerializer.Deserialize<PublishingPolicy>(setting.Value, JsonOptions) 
                   ?? new PublishingPolicy();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse publishing policy, using defaults");
            return new PublishingPolicy();
        }
    }

    public async Task<PublishingPolicy> UpdatePolicyAsync(PublishingPolicy policy, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(policy, JsonOptions);
        
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == PolicyKey, ct);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = PolicyKey,
                Value = json,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = json;
        }

        await _db.SaveChangesAsync(ct);
        
        _logger.LogInformation("Publishing policy updated");
        
        return policy;
    }

    public async Task<DailyPublishingStats> GetDailyStatsAsync(string platform, CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(ct);
        var platformPolicy = policy.Platforms.GetValueOrDefault(platform) ?? new PlatformPolicy();
        
        // Get timezone
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        // Calculate today's start in UTC
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayLocalStart = nowLocal.Date;
        var todayUtcStart = TimeZoneInfo.ConvertTimeToUtc(todayLocalStart, tz);
        var todayUtcEnd = todayUtcStart.AddDays(1);

        // Count published items for today
        var count = await _db.PublishJobs
            .Where(p => p.Status == PublishJobStatuses.Completed &&
                       p.CompletedAtUtc >= todayUtcStart &&
                       p.CompletedAtUtc < todayUtcEnd)
            .CountAsync(ct);

        // Note: For per-platform counting, we'd need to track platform in PublishJob
        // For now, this counts all platforms together

        return new DailyPublishingStats
        {
            Platform = platform,
            Date = todayLocalStart,
            Count = count,
            Limit = platformPolicy.DailyLimit
        };
    }

    public async Task<List<DailyPublishingStats>> GetAllDailyStatsAsync(CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(ct);
        var stats = new List<DailyPublishingStats>();

        foreach (var platform in policy.Platforms.Keys)
        {
            var platformStats = await GetDailyStatsAsync(platform, ct);
            stats.Add(platformStats);
        }

        return stats;
    }
}

