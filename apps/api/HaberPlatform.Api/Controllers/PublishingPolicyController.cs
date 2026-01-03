using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Publishing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/publishing/policy")]
[Authorize(Roles = "Admin")]
public class PublishingPolicyController : ControllerBase
{
    private readonly IPublishingPolicyService _policyService;
    private readonly IPublishScheduler _scheduler;
    private readonly ILogger<PublishingPolicyController> _logger;

    public PublishingPolicyController(
        IPublishingPolicyService policyService,
        IPublishScheduler scheduler,
        ILogger<PublishingPolicyController> logger)
    {
        _policyService = policyService;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>
    /// Get current publishing policy
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPolicy(CancellationToken ct)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        return Ok(policy);
    }

    /// <summary>
    /// Update publishing policy
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdatePolicy([FromBody] PublishingPolicy policy, CancellationToken ct)
    {
        var updated = await _policyService.UpdatePolicyAsync(policy, ct);
        return Ok(new { success = true, policy = updated });
    }

    /// <summary>
    /// Get platform-specific policy
    /// </summary>
    [HttpGet("platforms/{platform}")]
    public async Task<IActionResult> GetPlatformPolicy(string platform, CancellationToken ct)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        
        if (!policy.Platforms.TryGetValue(platform, out var platformPolicy))
        {
            return NotFound(new { error = $"Platform {platform} not found" });
        }

        return Ok(platformPolicy);
    }

    /// <summary>
    /// Update platform-specific policy
    /// </summary>
    [HttpPut("platforms/{platform}")]
    public async Task<IActionResult> UpdatePlatformPolicy(
        string platform,
        [FromBody] PlatformPolicy platformPolicy,
        CancellationToken ct)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        policy.Platforms[platform] = platformPolicy;
        
        await _policyService.UpdatePolicyAsync(policy, ct);
        
        return Ok(new { success = true, platform, policy = platformPolicy });
    }

    /// <summary>
    /// Get daily publishing stats for all platforms
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _policyService.GetAllDailyStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get daily stats for a specific platform
    /// </summary>
    [HttpGet("stats/{platform}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> GetPlatformStats(string platform, CancellationToken ct)
    {
        var stats = await _policyService.GetDailyStatsAsync(platform, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Preview schedule for a platform
    /// </summary>
    [HttpGet("schedule/preview/{platform}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> PreviewSchedule(
        string platform,
        [FromQuery] bool isEmergency = false,
        CancellationToken ct = default)
    {
        var schedule = await _scheduler.CalculateScheduleAsync(platform, isEmergency, ct);
        
        return Ok(new
        {
            platform,
            isEmergency,
            schedule.CanPublishNow,
            schedule.ScheduledAtUtc,
            schedule.Reason,
            schedule.SilencePush,
            schedule.DailyCountSoFar,
            schedule.DailyLimit,
            isNightMode = await _scheduler.IsNightModeAsync(platform, DateTime.UtcNow, ct),
            isWithinWindow = await _scheduler.IsWithinWindowAsync(platform, DateTime.UtcNow, ct),
            nextSlot = await _scheduler.GetNextSlotAsync(platform, ct)
        });
    }

    /// <summary>
    /// Get emergency rules
    /// </summary>
    [HttpGet("emergency-rules")]
    public async Task<IActionResult> GetEmergencyRules(CancellationToken ct)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        return Ok(policy.EmergencyRules);
    }

    /// <summary>
    /// Update emergency rules
    /// </summary>
    [HttpPut("emergency-rules")]
    public async Task<IActionResult> UpdateEmergencyRules(
        [FromBody] EmergencyRules rules,
        CancellationToken ct)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        policy.EmergencyRules = rules;
        
        await _policyService.UpdatePolicyAsync(policy, ct);
        
        return Ok(new { success = true, rules });
    }
}

