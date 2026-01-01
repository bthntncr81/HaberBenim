using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/feed")]
[Authorize]
public class FeedController : ControllerBase
{
    private readonly AppDbContext _db;

    public FeedController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetFeed(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? sourceId,
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? decisionType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _db.ContentItems
            .AsNoTracking() // Performance: don't track entities
            .Include(c => c.Source) // Needed for Source.Name in Select
            .Where(c => c.Status != ContentStatuses.Duplicate) // Exclude duplicates from main feed
            .AsQueryable();

        // Apply filters
        if (fromUtc.HasValue)
            query = query.Where(c => c.PublishedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(c => c.PublishedAtUtc <= toUtc.Value);

        if (sourceId.HasValue)
            query = query.Where(c => c.SourceId == sourceId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(decisionType))
            query = query.Where(c => c.DecisionType == decisionType);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, $"%{keyword}%") ||
                (c.Summary != null && EF.Functions.ILike(c.Summary, $"%{keyword}%")) ||
                EF.Functions.ILike(c.BodyText, $"%{keyword}%"));
        }

        // Get total count
        var total = await query.CountAsync();

        // Apply pagination and ordering
        var items = await query
            .OrderByDescending(c => c.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new FeedItemDto(
                c.Id,
                c.PublishedAtUtc,
                c.SourceId,
                c.Source.Name,
                c.Title,
                c.Summary,
                c.CanonicalUrl,
                c.Status,
                c.DuplicateCount,
                c.DecisionType,
                c.DecidedByRuleId,
                c.DecisionReason,
                c.DecidedAtUtc,
                c.ScheduledAtUtc,
                c.TrustLevelSnapshot,
                c.IsTruncated,
                c.ContentText
            ))
            .ToListAsync();

        return Ok(new FeedResponse(items, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFeedItem(Guid id)
    {
        var item = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Media)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null)
            return NotFound(new { error = "Content item not found" });

        var dto = new FeedItemDetailDto(
            item.Id,
            item.PublishedAtUtc,
            item.IngestedAtUtc,
            item.SourceId,
            item.Source.Name,
            item.Title,
            item.Summary,
            item.BodyText,
            item.OriginalText,
            item.CanonicalUrl,
            item.Language,
            item.Status,
            item.DuplicateCount,
            item.DecisionType,
            item.DecidedByRuleId,
            item.DecisionReason,
            item.DecidedAtUtc,
            item.ScheduledAtUtc,
            item.TrustLevelSnapshot,
            item.Media.Select(m => new MediaDto(
                m.Id,
                m.MediaType,
                m.Url,
                m.ThumbUrl
            )).ToList(),
            item.IsTruncated,
            item.ContentHtml,
            item.ContentText,
            item.ArticleFetchError
        );

        return Ok(dto);
    }
}
