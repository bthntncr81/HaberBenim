using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/sources")]
[Authorize]
public class SourcesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SourcesController> _logger;

    public SourcesController(AppDbContext db, ILogger<SourcesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// List sources with filtering, search, and pagination
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListSources([FromQuery] SourceQueryParams query)
    {
        var q = _db.Sources.AsQueryable();

        // Filter by type
        if (!string.IsNullOrWhiteSpace(query.Type))
            q = q.Where(s => s.Type == query.Type);

        // Filter by category
        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(s => s.Category == query.Category);

        // Filter by active status
        if (query.IsActive.HasValue)
            q = q.Where(s => s.IsActive == query.IsActive.Value);

        // Search in name or identifier
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var searchTerm = query.Q.ToLower();
            q = q.Where(s => 
                s.Name.ToLower().Contains(searchTerm) ||
                (s.Identifier != null && s.Identifier.ToLower().Contains(searchTerm))
            );
        }

        // Get total count
        var total = await q.CountAsync();

        // Apply pagination and ordering
        var items = await q
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new SourceListItemDto(
                s.Id,
                s.Name,
                s.Type,
                s.Identifier,
                s.Url,
                s.Category,
                s.TrustLevel,
                s.Priority,
                s.IsActive,
                s.DefaultBehavior,
                s.UpdatedAtUtc
            ))
            .ToListAsync();

        return Ok(new SourceListResponse(items, total, query.Page, query.PageSize));
    }

    /// <summary>
    /// Get a source by ID with full details
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSource(Guid id)
    {
        var source = await _db.Sources
            .Include(s => s.XSourceState)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (source == null)
            return NotFound(new { error = "Source not found" });

        return Ok(MapToDetailDto(source));
    }

    /// <summary>
    /// Create a new source (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSource([FromBody] UpsertSourceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Check for unique name
        if (await _db.Sources.AnyAsync(s => s.Name == request.Name))
            return Conflict(new { error = "Source with this name already exists" });

        // Check for unique URL for RSS sources
        if (request.Type == SourceTypes.RSS && !string.IsNullOrWhiteSpace(request.Url))
        {
            if (await _db.Sources.AnyAsync(s => s.Type == SourceTypes.RSS && s.Url == request.Url))
                return Conflict(new { error = "An RSS source with this URL already exists" });
        }

        // Check for unique identifier for X sources
        if (request.Type == SourceTypes.X && !string.IsNullOrWhiteSpace(request.Identifier))
        {
            var cleanIdentifier = request.Identifier.TrimStart('@').Trim();
            if (await _db.Sources.AnyAsync(s => s.Type == SourceTypes.X && s.Identifier == cleanIdentifier))
                return Conflict(new { error = "An X source with this identifier already exists" });
        }

        var source = new Source
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Identifier = request.Type == SourceTypes.X 
                ? request.Identifier?.TrimStart('@').Trim() 
                : request.Identifier,
            Url = request.Url,
            Description = request.Description,
            Category = request.Category,
            TrustLevel = request.TrustLevel,
            Priority = request.Priority,
            IsActive = request.IsActive,
            DefaultBehavior = request.DefaultBehavior,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Sources.Add(source);

        // Create XSourceState for X sources
        if (source.Type == SourceTypes.X)
        {
            var xState = new XSourceState
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id
            };
            _db.XSourceStates.Add(xState);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Source created: {SourceId} {SourceName} ({Type})", 
            source.Id, source.Name, source.Type);

        // Reload with XSourceState
        source = await _db.Sources
            .Include(s => s.XSourceState)
            .FirstAsync(s => s.Id == source.Id);

        return CreatedAtAction(nameof(GetSource), new { id = source.Id }, MapToDetailDto(source));
    }

    /// <summary>
    /// Update a source (Admin only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSource(Guid id, [FromBody] UpsertSourceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var source = await _db.Sources
            .Include(s => s.XSourceState)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (source == null)
            return NotFound(new { error = "Source not found" });

        // Check for unique name (excluding this source)
        if (await _db.Sources.AnyAsync(s => s.Id != id && s.Name == request.Name))
            return Conflict(new { error = "Source with this name already exists" });

        // Check for unique URL for RSS sources (excluding this source)
        if (request.Type == SourceTypes.RSS && !string.IsNullOrWhiteSpace(request.Url))
        {
            if (await _db.Sources.AnyAsync(s => s.Id != id && s.Type == SourceTypes.RSS && s.Url == request.Url))
                return Conflict(new { error = "An RSS source with this URL already exists" });
        }

        // Check for unique identifier for X sources (excluding this source)
        if (request.Type == SourceTypes.X && !string.IsNullOrWhiteSpace(request.Identifier))
        {
            var cleanIdentifier = request.Identifier.TrimStart('@').Trim();
            if (await _db.Sources.AnyAsync(s => s.Id != id && s.Type == SourceTypes.X && s.Identifier == cleanIdentifier))
                return Conflict(new { error = "An X source with this identifier already exists" });
        }

        // Check if type is changing from/to X and handle XSourceState
        var wasX = source.Type == SourceTypes.X;
        var isNowX = request.Type == SourceTypes.X;

        source.Name = request.Name;
        source.Type = request.Type;
        source.Identifier = request.Type == SourceTypes.X 
            ? request.Identifier?.TrimStart('@').Trim() 
            : request.Identifier;
        source.Url = request.Url;
        source.Description = request.Description;
        source.Category = request.Category;
        source.TrustLevel = request.TrustLevel;
        source.Priority = request.Priority;
        source.IsActive = request.IsActive;
        source.DefaultBehavior = request.DefaultBehavior;
        source.UpdatedAtUtc = DateTime.UtcNow;

        // Handle XSourceState based on type change
        if (!wasX && isNowX && source.XSourceState == null)
        {
            // Create XSourceState for newly X source
            var xState = new XSourceState
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id
            };
            _db.XSourceStates.Add(xState);
        }
        else if (wasX && !isNowX && source.XSourceState != null)
        {
            // Remove XSourceState if no longer X type
            _db.XSourceStates.Remove(source.XSourceState);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Source updated: {SourceId} {SourceName}", source.Id, source.Name);

        // Reload with XSourceState
        source = await _db.Sources
            .Include(s => s.XSourceState)
            .FirstAsync(s => s.Id == source.Id);

        return Ok(MapToDetailDto(source));
    }

    /// <summary>
    /// Delete a source (Admin only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSource(Guid id)
    {
        var source = await _db.Sources.FindAsync(id);
        if (source == null)
            return NotFound(new { error = "Source not found" });

        _db.Sources.Remove(source);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Source deleted: {SourceId} {SourceName}", source.Id, source.Name);

        return NoContent();
    }

    /// <summary>
    /// Toggle source active status (Admin only)
    /// </summary>
    [HttpPost("{id:guid}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActive(Guid id, [FromBody] ToggleActiveRequest request)
    {
        var source = await _db.Sources
            .Include(s => s.XSourceState)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (source == null)
            return NotFound(new { error = "Source not found" });

        source.IsActive = request.IsActive;
        source.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Source {Action}: {SourceId} {SourceName}", 
            request.IsActive ? "activated" : "deactivated", source.Id, source.Name);

        return Ok(MapToDetailDto(source));
    }

    /// <summary>
    /// Get X source state (for X-type sources only)
    /// </summary>
    [HttpGet("{id:guid}/x-state")]
    public async Task<IActionResult> GetXState(Guid id)
    {
        var source = await _db.Sources
            .Include(s => s.XSourceState)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (source == null)
            return NotFound(new { error = "Source not found" });

        if (source.Type != SourceTypes.X)
            return BadRequest(new { error = "Source is not an X source" });

        if (source.XSourceState == null)
            return Ok(null);

        return Ok(new XSourceStateDto(
            source.XSourceState.Id,
            source.XSourceState.XUserId,
            source.XSourceState.LastSinceId,
            source.XSourceState.LastPolledAtUtc,
            source.XSourceState.LastSuccessAtUtc,
            source.XSourceState.LastFailureAtUtc,
            source.XSourceState.LastError,
            source.XSourceState.ConsecutiveFailures
        ));
    }

    /// <summary>
    /// Get all categories (for dropdown/filter)
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Sources
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(categories);
    }

    private static SourceDetailDto MapToDetailDto(Source source)
    {
        return new SourceDetailDto(
            source.Id,
            source.Name,
            source.Type,
            source.Identifier,
            source.Url,
            source.Description,
            source.Category,
            source.Group,
            source.TrustLevel,
            source.Priority,
            source.IsActive,
            source.DefaultBehavior,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.LastFetchedAtUtc,
            source.FetchIntervalMinutes,
            source.XSourceState != null
                ? new XSourceStateDto(
                    source.XSourceState.Id,
                    source.XSourceState.XUserId,
                    source.XSourceState.LastSinceId,
                    source.XSourceState.LastPolledAtUtc,
                    source.XSourceState.LastSuccessAtUtc,
                    source.XSourceState.LastFailureAtUtc,
                    source.XSourceState.LastError,
                    source.XSourceState.ConsecutiveFailures
                )
                : null
        );
    }
}
