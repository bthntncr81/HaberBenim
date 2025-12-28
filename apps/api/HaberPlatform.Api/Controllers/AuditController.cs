using HaberPlatform.Api.Data;
using HaberPlatform.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// Audit log viewer endpoints (Admin only)
/// </summary>
[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "Admin")]
[Tags("Audit")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditController> _logger;

    public AuditController(AppDbContext db, ILogger<AuditController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with filters
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(AuditLogListResponse), 200)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? userEmail,
        [FromQuery] string? path,
        [FromQuery] int? statusCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrEmpty(userEmail))
        {
            query = query.Where(l => l.UserEmail != null && l.UserEmail.Contains(userEmail));
        }

        if (!string.IsNullOrEmpty(path))
        {
            query = query.Where(l => l.Path.Contains(path));
        }

        if (statusCode.HasValue)
        {
            query = query.Where(l => l.StatusCode == statusCode.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto
            {
                Id = l.Id,
                CreatedAtUtc = l.CreatedAtUtc,
                UserId = l.UserId,
                UserEmail = l.UserEmail,
                Method = l.Method,
                Path = l.Path,
                StatusCode = l.StatusCode,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                DurationMs = l.DurationMs
            })
            .ToListAsync();

        return Ok(new AuditLogListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get audit log statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-7);
        var to = toUtc ?? DateTime.UtcNow;

        var query = _db.AuditLogs
            .Where(l => l.CreatedAtUtc >= from && l.CreatedAtUtc <= to);

        var totalRequests = await query.CountAsync();
        var avgDuration = await query.AverageAsync(l => (double?)l.DurationMs) ?? 0;
        var errorCount = await query.CountAsync(l => l.StatusCode >= 400);
        var uniqueUsers = await query.Select(l => l.UserEmail).Distinct().CountAsync();

        return Ok(new
        {
            fromUtc = from,
            toUtc = to,
            totalRequests,
            avgDurationMs = Math.Round(avgDuration, 2),
            errorCount,
            uniqueUsers
        });
    }
}

