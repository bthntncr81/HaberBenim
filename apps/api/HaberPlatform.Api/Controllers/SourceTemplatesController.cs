using System.Text.Json;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/source-templates")]
[Authorize(Roles = "Admin")]
public class SourceTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SourceTemplatesController> _logger;

    public SourceTemplatesController(AppDbContext db, ILogger<SourceTemplatesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// List source template assignments
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SourceTemplateAssignmentListQuery query, CancellationToken ct)
    {
        var q = _db.SourceTemplateAssignments
            .Include(a => a.Source)
            .Include(a => a.Template)
            .AsQueryable();

        if (query.SourceId.HasValue)
            q = q.Where(a => a.SourceId == query.SourceId.Value);

        if (!string.IsNullOrEmpty(query.Platform))
            q = q.Where(a => a.Platform == query.Platform);

        if (query.TemplateId.HasValue)
            q = q.Where(a => a.TemplateId == query.TemplateId.Value);

        if (query.Active.HasValue)
            q = q.Where(a => a.IsActive == query.Active.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(a => a.Source.Name)
            .ThenBy(a => a.Platform)
            .ThenBy(a => a.PriorityOverride ?? a.Template.Priority)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new SourceTemplateAssignmentDto
            {
                Id = a.Id,
                SourceId = a.SourceId,
                SourceName = a.Source.Name,
                Platform = a.Platform,
                Mode = a.Mode,
                TemplateId = a.TemplateId,
                TemplateName = a.Template.Name,
                TemplateFormat = a.Template.Format,
                PriorityOverride = a.PriorityOverride,
                EffectivePriority = a.PriorityOverride ?? a.Template.Priority,
                IsActive = a.IsActive,
                CreatedAtUtc = a.CreatedAtUtc,
                UpdatedAtUtc = a.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new
        {
            items,
            total,
            page = query.Page,
            pageSize = query.PageSize,
            totalPages = (int)Math.Ceiling(total / (double)query.PageSize)
        });
    }

    /// <summary>
    /// Get assignment by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var assignment = await _db.SourceTemplateAssignments
            .Include(a => a.Source)
            .Include(a => a.Template)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment == null)
            return NotFound(new { error = "Assignment not found" });

        return Ok(new SourceTemplateAssignmentDto
        {
            Id = assignment.Id,
            SourceId = assignment.SourceId,
            SourceName = assignment.Source.Name,
            Platform = assignment.Platform,
            Mode = assignment.Mode,
            TemplateId = assignment.TemplateId,
            TemplateName = assignment.Template.Name,
            TemplateFormat = assignment.Template.Format,
            PriorityOverride = assignment.PriorityOverride,
            EffectivePriority = assignment.PriorityOverride ?? assignment.Template.Priority,
            IsActive = assignment.IsActive,
            CreatedAtUtc = assignment.CreatedAtUtc,
            UpdatedAtUtc = assignment.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Create new assignment
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSourceTemplateAssignmentRequest request, CancellationToken ct)
    {
        // Validate platform
        if (!TemplatePlatforms.All.Contains(request.Platform))
        {
            return BadRequest(new { error = $"Invalid platform. Allowed: {string.Join(", ", TemplatePlatforms.All)}" });
        }

        // Check source exists
        var source = await _db.Sources.FindAsync([request.SourceId], ct);
        if (source == null)
            return BadRequest(new { error = "Source not found" });

        // Check template exists
        var template = await _db.PublishTemplates.FindAsync([request.TemplateId], ct);
        if (template == null)
            return BadRequest(new { error = "Template not found" });

        // Check platform matches
        if (template.Platform != request.Platform)
        {
            return BadRequest(new { error = $"Template platform ({template.Platform}) does not match requested platform ({request.Platform})" });
        }

        // Check for duplicate
        var exists = await _db.SourceTemplateAssignments.AnyAsync(a =>
            a.SourceId == request.SourceId &&
            a.Platform == request.Platform &&
            a.TemplateId == request.TemplateId, ct);

        if (exists)
        {
            return BadRequest(new { error = "This source-template assignment already exists" });
        }

        var assignment = new SourceTemplateAssignment
        {
            Id = Guid.NewGuid(),
            SourceId = request.SourceId,
            Platform = request.Platform,
            Mode = "Auto",
            TemplateId = request.TemplateId,
            PriorityOverride = request.PriorityOverride,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.SourceTemplateAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created source template assignment {Id}: Source {SourceId} -> Template {TemplateId} ({Platform})",
            assignment.Id, request.SourceId, request.TemplateId, request.Platform);

        return CreatedAtAction(nameof(Get), new { id = assignment.Id }, new SourceTemplateAssignmentDto
        {
            Id = assignment.Id,
            SourceId = assignment.SourceId,
            SourceName = source.Name,
            Platform = assignment.Platform,
            Mode = assignment.Mode,
            TemplateId = assignment.TemplateId,
            TemplateName = template.Name,
            TemplateFormat = template.Format,
            PriorityOverride = assignment.PriorityOverride,
            EffectivePriority = assignment.PriorityOverride ?? template.Priority,
            IsActive = assignment.IsActive,
            CreatedAtUtc = assignment.CreatedAtUtc,
            UpdatedAtUtc = assignment.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Update assignment
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSourceTemplateAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await _db.SourceTemplateAssignments.FindAsync([id], ct);
        if (assignment == null)
            return NotFound(new { error = "Assignment not found" });

        if (request.PriorityOverride.HasValue)
            assignment.PriorityOverride = request.PriorityOverride.Value == 0 ? null : request.PriorityOverride;

        if (request.IsActive.HasValue)
            assignment.IsActive = request.IsActive.Value;

        assignment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated source template assignment {Id}", id);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete assignment
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var assignment = await _db.SourceTemplateAssignments.FindAsync([id], ct);
        if (assignment == null)
            return NotFound(new { error = "Assignment not found" });

        _db.SourceTemplateAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted source template assignment {Id}", id);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Bulk assign a template to multiple sources
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkAssign([FromBody] BulkAssignTemplateRequest request, CancellationToken ct)
    {
        // Validate platform
        if (!TemplatePlatforms.All.Contains(request.Platform))
        {
            return BadRequest(new { error = $"Invalid platform. Allowed: {string.Join(", ", TemplatePlatforms.All)}" });
        }

        // Check template exists and matches platform
        var template = await _db.PublishTemplates.FindAsync([request.TemplateId], ct);
        if (template == null)
            return BadRequest(new { error = "Template not found" });

        if (template.Platform != request.Platform)
        {
            return BadRequest(new { error = $"Template platform ({template.Platform}) does not match requested platform ({request.Platform})" });
        }

        var created = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var sourceId in request.SourceIds)
        {
            // Check if already exists
            var exists = await _db.SourceTemplateAssignments.AnyAsync(a =>
                a.SourceId == sourceId &&
                a.Platform == request.Platform &&
                a.TemplateId == request.TemplateId, ct);

            if (exists)
            {
                skipped++;
                continue;
            }

            // Check source exists
            var source = await _db.Sources.FindAsync([sourceId], ct);
            if (source == null)
            {
                errors.Add($"Source {sourceId} not found");
                continue;
            }

            var assignment = new SourceTemplateAssignment
            {
                Id = Guid.NewGuid(),
                SourceId = sourceId,
                Platform = request.Platform,
                Mode = "Auto",
                TemplateId = request.TemplateId,
                PriorityOverride = request.PriorityOverride,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.SourceTemplateAssignments.Add(assignment);
            created++;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Bulk assigned template {TemplateId} to {Created} sources ({Skipped} skipped)",
            request.TemplateId, created, skipped);

        return Ok(new BulkAssignTemplateResponse
        {
            Created = created,
            Skipped = skipped,
            Errors = errors
        });
    }

    /// <summary>
    /// Get assignments for a specific source
    /// </summary>
    [HttpGet("by-source/{sourceId:guid}")]
    public async Task<IActionResult> GetBySource(Guid sourceId, CancellationToken ct)
    {
        var assignments = await _db.SourceTemplateAssignments
            .Include(a => a.Template)
            .Where(a => a.SourceId == sourceId)
            .OrderBy(a => a.Platform)
            .ThenBy(a => a.PriorityOverride ?? a.Template.Priority)
            .Select(a => new SourceTemplateAssignmentDto
            {
                Id = a.Id,
                SourceId = a.SourceId,
                SourceName = "",
                Platform = a.Platform,
                Mode = a.Mode,
                TemplateId = a.TemplateId,
                TemplateName = a.Template.Name,
                TemplateFormat = a.Template.Format,
                PriorityOverride = a.PriorityOverride,
                EffectivePriority = a.PriorityOverride ?? a.Template.Priority,
                IsActive = a.IsActive,
                CreatedAtUtc = a.CreatedAtUtc,
                UpdatedAtUtc = a.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new { items = assignments });
    }
}

