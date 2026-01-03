using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services.Publishing;

public interface IPublishScheduler
{
    /// <summary>
    /// Calculate when a content item should be published
    /// </summary>
    Task<ScheduleResult> CalculateScheduleAsync(
        string platform,
        bool isEmergency = false,
        CancellationToken ct = default);
    
    /// <summary>
    /// Check if we're within allowed publishing window
    /// </summary>
    Task<bool> IsWithinWindowAsync(string platform, DateTime utcTime, CancellationToken ct = default);
    
    /// <summary>
    /// Check if we're in night mode
    /// </summary>
    Task<bool> IsNightModeAsync(string platform, DateTime utcTime, CancellationToken ct = default);
    
    /// <summary>
    /// Get next available publishing slot
    /// </summary>
    Task<DateTime> GetNextSlotAsync(string platform, CancellationToken ct = default);
}

public class PublishScheduler : IPublishScheduler
{
    private readonly AppDbContext _db;
    private readonly IPublishingPolicyService _policyService;
    private readonly ILogger<PublishScheduler> _logger;

    public PublishScheduler(
        AppDbContext db,
        IPublishingPolicyService policyService,
        ILogger<PublishScheduler> logger)
    {
        _db = db;
        _policyService = policyService;
        _logger = logger;
    }

    public async Task<ScheduleResult> CalculateScheduleAsync(
        string platform,
        bool isEmergency = false,
        CancellationToken ct = default)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        var platformPolicy = policy.Platforms.GetValueOrDefault(platform);

        if (platformPolicy == null || !platformPolicy.IsEnabled)
        {
            return ScheduleResult.Scheduled(
                DateTime.MaxValue,
                $"Platform {platform} is disabled");
        }

        var tz = GetTimeZone(policy.TimeZoneId);
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

        // Check if emergency override
        if (isEmergency && platformPolicy.EmergencyOverride)
        {
            _logger.LogInformation("Emergency override for {Platform}", platform);
            
            // Check night mode for push silencing
            bool silencePush = platformPolicy.NightMode.IsNightTime(nowLocal.TimeOfDay);
            
            return ScheduleResult.Now(isEmergency: true, silencePush: silencePush);
        }

        // Check daily limit
        var dailyStats = await _policyService.GetDailyStatsAsync(platform, ct);
        if (platformPolicy.DailyLimit > 0 && dailyStats.Count >= platformPolicy.DailyLimit)
        {
            // Schedule for tomorrow's first window
            var nextSlot = await GetNextSlotAsync(platform, ct);
            return ScheduleResult.Scheduled(
                nextSlot,
                $"Daily limit reached ({dailyStats.Count}/{platformPolicy.DailyLimit})",
                dailyStats.Count,
                platformPolicy.DailyLimit);
        }

        // Check if within allowed window
        bool inWindow = IsWithinWindow(platformPolicy, nowLocal.TimeOfDay);
        if (!inWindow)
        {
            var nextSlot = await GetNextSlotAsync(platform, ct);
            return ScheduleResult.Scheduled(
                nextSlot,
                "Outside publishing window");
        }

        // Check minimum interval
        var lastPublish = await GetLastPublishTimeAsync(platform, ct);
        if (lastPublish.HasValue)
        {
            var minInterval = TimeSpan.FromMinutes(platformPolicy.MinIntervalMinutes);
            var nextAllowed = lastPublish.Value.Add(minInterval);
            
            if (nowUtc < nextAllowed)
            {
                return ScheduleResult.Scheduled(
                    nextAllowed,
                    $"Rate limit: {platformPolicy.MinIntervalMinutes} min between posts");
            }
        }

        // Check night mode
        bool isNightMode = platformPolicy.NightMode.IsNightTime(nowLocal.TimeOfDay);
        if (isNightMode && platformPolicy.NightMode.QueueForMorning && !isEmergency)
        {
            var nextSlot = await GetNextSlotAsync(platform, ct);
            return ScheduleResult.Scheduled(
                nextSlot,
                "Night mode: queued for morning");
        }

        // Can publish now
        return ScheduleResult.Now(
            isEmergency: false,
            silencePush: isNightMode && platformPolicy.NightMode.SilencePush);
    }

    public async Task<bool> IsWithinWindowAsync(string platform, DateTime utcTime, CancellationToken ct = default)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        var platformPolicy = policy.Platforms.GetValueOrDefault(platform);
        
        if (platformPolicy == null)
            return false;

        var tz = GetTimeZone(policy.TimeZoneId);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        
        return IsWithinWindow(platformPolicy, localTime.TimeOfDay);
    }

    public async Task<bool> IsNightModeAsync(string platform, DateTime utcTime, CancellationToken ct = default)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        var platformPolicy = policy.Platforms.GetValueOrDefault(platform);
        
        if (platformPolicy == null)
            return false;

        var tz = GetTimeZone(policy.TimeZoneId);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        
        return platformPolicy.NightMode.IsNightTime(localTime.TimeOfDay);
    }

    public async Task<DateTime> GetNextSlotAsync(string platform, CancellationToken ct = default)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        var platformPolicy = policy.Platforms.GetValueOrDefault(platform);
        
        if (platformPolicy == null || platformPolicy.AllowedWindows.Count == 0)
        {
            // No windows defined, next slot is now + min interval
            return DateTime.UtcNow.AddMinutes(platformPolicy?.MinIntervalMinutes ?? 30);
        }

        var tz = GetTimeZone(policy.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var currentTime = nowLocal.TimeOfDay;

        // Find the next window start
        DateTime? nextSlot = null;

        foreach (var window in platformPolicy.AllowedWindows.OrderBy(w => w.StartTime))
        {
            if (window.Contains(currentTime))
            {
                // We're in a window, can publish now (if other checks pass)
                // Return now + min interval as next slot
                var nextTime = nowLocal.AddMinutes(platformPolicy.MinIntervalMinutes);
                nextSlot = TimeZoneInfo.ConvertTimeToUtc(nextTime, tz);
                break;
            }
            
            if (window.StartTime > currentTime)
            {
                // Next window starts later today
                var nextWindowStart = nowLocal.Date.Add(window.StartTime);
                nextSlot = TimeZoneInfo.ConvertTimeToUtc(nextWindowStart, tz);
                break;
            }
        }

        if (!nextSlot.HasValue)
        {
            // No more windows today, schedule for tomorrow's first window
            var firstWindow = platformPolicy.AllowedWindows.OrderBy(w => w.StartTime).First();
            var tomorrowSlot = nowLocal.Date.AddDays(1).Add(firstWindow.StartTime);
            nextSlot = TimeZoneInfo.ConvertTimeToUtc(tomorrowSlot, tz);
        }

        // Also check daily limit reset
        var dailyStats = await _policyService.GetDailyStatsAsync(platform, ct);
        if (platformPolicy.DailyLimit > 0 && dailyStats.Count >= platformPolicy.DailyLimit)
        {
            // Schedule for tomorrow
            var tomorrowStart = nowLocal.Date.AddDays(1);
            var firstWindow = platformPolicy.AllowedWindows.OrderBy(w => w.StartTime).First();
            var tomorrowSlot = TimeZoneInfo.ConvertTimeToUtc(
                tomorrowStart.Add(firstWindow.StartTime), tz);
            
            if (!nextSlot.HasValue || tomorrowSlot > nextSlot.Value)
            {
                nextSlot = tomorrowSlot;
            }
        }

        return nextSlot ?? DateTime.UtcNow.AddHours(1);
    }

    private bool IsWithinWindow(PlatformPolicy policy, TimeSpan timeOfDay)
    {
        if (policy.AllowedWindows.Count == 0)
            return true; // No windows = always allowed

        return policy.AllowedWindows.Any(w => w.Contains(timeOfDay));
    }

    private async Task<DateTime?> GetLastPublishTimeAsync(string platform, CancellationToken ct)
    {
        // Get the last completed publish job
        // Note: For per-platform tracking, PublishJob would need a Platform field
        var lastJob = await _db.PublishJobs
            .Where(p => p.Status == PublishJobStatuses.Completed)
            .OrderByDescending(p => p.CompletedAtUtc)
            .Select(p => p.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        return lastJob;
    }

    private static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}

