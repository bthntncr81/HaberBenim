using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// Breaking news management endpoints
/// </summary>
[ApiController]
[Route("api/v1/breaking")]
[Authorize(Roles = "Admin,Editor")]
[Tags("Breaking News")]
public class BreakingController : ControllerBase
{
    private readonly BreakingNewsService _breakingService;
    private readonly ILogger<BreakingController> _logger;

    public BreakingController(
        BreakingNewsService breakingService,
        ILogger<BreakingController> logger)
    {
        _breakingService = breakingService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Mark content as breaking news
    /// </summary>
    [HttpPost("mark/{contentId:guid}")]
    [ProducesResponseType(typeof(MarkBreakingResponse), 200)]
    public async Task<IActionResult> MarkAsBreaking(
        Guid contentId,
        [FromBody] MarkBreakingRequest request)
    {
        var userId = GetUserId();
        var result = await _breakingService.MarkAsBreakingAsync(contentId, request, userId);

        if (!result.Ok)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get breaking news inbox
    /// </summary>
    [HttpGet("inbox")]
    [ProducesResponseType(typeof(BreakingInboxResponse), 200)]
    public async Task<IActionResult> GetInbox(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _breakingService.GetBreakingInboxAsync(new BreakingInboxParams
        {
            Status = status,
            Page = page,
            PageSize = pageSize
        });

        return Ok(result);
    }

    /// <summary>
    /// Re-enqueue publish job for breaking content (publish now)
    /// </summary>
    [HttpPost("publish-now/{contentId:guid}")]
    [ProducesResponseType(typeof(MarkBreakingResponse), 200)]
    public async Task<IActionResult> PublishNow(Guid contentId)
    {
        var userId = GetUserId();
        var result = await _breakingService.PublishNowAsync(contentId, userId);

        if (!result.Ok)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

