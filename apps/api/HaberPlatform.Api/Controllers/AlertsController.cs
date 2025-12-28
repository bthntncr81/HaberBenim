using HaberPlatform.Api.Data;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// Admin alerts management endpoints
/// </summary>
[ApiController]
[Route("api/v1/alerts")]
[Authorize(Roles = "Admin,Editor")]
[Tags("Alerts")]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AlertService _alertService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        AppDbContext db,
        AlertService alertService,
        ILogger<AlertsController> logger)
    {
        _db = db;
        _alertService = alertService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Get alerts list with filters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AlertListResponse), 200)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] string? severity,
        [FromQuery] string? type,
        [FromQuery] bool? acknowledged,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.AdminAlerts
            .Include(a => a.AcknowledgedByUser)
            .AsQueryable();

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(a => a.Severity == severity);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(a => a.Type == type);
        }

        if (acknowledged.HasValue)
        {
            query = query.Where(a => a.IsAcknowledged == acknowledged.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.CreatedAtUtc <= toUtc.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminAlertDto
            {
                Id = a.Id,
                CreatedAtUtc = a.CreatedAtUtc,
                Type = a.Type,
                Severity = a.Severity,
                Title = a.Title,
                Message = a.Message,
                IsAcknowledged = a.IsAcknowledged,
                AcknowledgedAtUtc = a.AcknowledgedAtUtc,
                AcknowledgedByUserEmail = a.AcknowledgedByUser != null ? a.AcknowledgedByUser.Email : null,
                MetaJson = a.MetaJson
            })
            .ToListAsync();

        return Ok(new AlertListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Acknowledge an alert (Admin only)
    /// </summary>
    [HttpPost("{id:guid}/ack")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AlertAckResponse), 200)]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var userId = GetUserId();
        var success = await _alertService.AcknowledgeAlertAsync(id, userId);

        if (!success)
        {
            return NotFound(new AlertAckResponse { Ok = false, Error = "Alert not found" });
        }

        return Ok(new AlertAckResponse { Ok = true });
    }

    /// <summary>
    /// Get unacknowledged alert count
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetUnacknowledgedCount()
    {
        var count = await _db.AdminAlerts
            .Where(a => !a.IsAcknowledged)
            .CountAsync();

        var criticalCount = await _db.AdminAlerts
            .Where(a => !a.IsAcknowledged && a.Severity == "Critical")
            .CountAsync();

        return Ok(new { total = count, critical = criticalCount });
    }
}

