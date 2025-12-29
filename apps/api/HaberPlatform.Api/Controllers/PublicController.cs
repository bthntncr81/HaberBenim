using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// Public API for published content (no auth required)
/// </summary>
[ApiController]
[Route("api/v1/public")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    private const int MaxExcerptLength = 180;

    public PublicController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get latest published articles
    /// </summary>
    [HttpGet("latest")]
    public async Task<ActionResult<PublicArticleListResponse>> GetLatest([FromQuery] PublicArticleQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.PublishedContents
            .Where(p => !p.IsRetracted) // Exclude retracted content
            .AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var searchTerm = query.Q.Trim();
            q = q.Where(p => 
                EF.Functions.ILike(p.WebTitle, $"%{searchTerm}%") ||
                EF.Functions.ILike(p.WebBody, $"%{searchTerm}%"));
        }

        // Date filters
        if (query.FromUtc.HasValue)
        {
            q = q.Where(p => p.PublishedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            q = q.Where(p => p.PublishedAtUtc <= query.ToUtc.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(p => p.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PublicArticleListItemDto(
                p.Id,
                p.WebTitle,
                CreateExcerpt(p.WebBody),
                p.PublishedAtUtc,
                p.Path,
                p.CanonicalUrl,
                p.SourceName,
                p.CategoryOrGroup,
                p.PrimaryImageUrl
            ))
            .ToListAsync();

        return Ok(new PublicArticleListResponse(items, total, page, pageSize));
    }

    /// <summary>
    /// Search published articles
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PublicArticleListResponse>> Search([FromQuery] PublicArticleQueryParams query)
    {
        // Reuse latest with q parameter
        return await GetLatest(query);
    }

    /// <summary>
    /// Get a single published article by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicArticleDto>> GetArticle(Guid id)
    {
        var article = await _db.PublishedContents
            .Where(p => p.Id == id && !p.IsRetracted) // Return 404 for retracted content
            .Select(p => new PublicArticleDto(
                p.Id,
                p.WebTitle,
                p.WebBody,
                p.PublishedAtUtc,
                p.Path,
                p.Slug,
                p.CanonicalUrl,
                p.SourceName,
                p.CategoryOrGroup,
                p.PrimaryImageUrl
            ))
            .FirstOrDefaultAsync();

        if (article == null)
        {
            return NotFound(new { error = "Article not found" });
        }

        return Ok(article);
    }

    /// <summary>
    /// Get a single published article by slug/path
    /// </summary>
    [HttpGet("by-path")]
    public async Task<ActionResult<PublicArticleDto>> GetByPath([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "Path is required" });
        }

        var article = await _db.PublishedContents
            .Where(p => p.Path == path && !p.IsRetracted) // Return 404 for retracted content
            .Select(p => new PublicArticleDto(
                p.Id,
                p.WebTitle,
                p.WebBody,
                p.PublishedAtUtc,
                p.Path,
                p.Slug,
                p.CanonicalUrl,
                p.SourceName,
                p.CategoryOrGroup,
                p.PrimaryImageUrl
            ))
            .FirstOrDefaultAsync();

        if (article == null)
        {
            return NotFound(new { error = "Article not found" });
        }

        return Ok(article);
    }

    /// <summary>
    /// Create an excerpt from HTML/text body
    /// </summary>
    private static string CreateExcerpt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        // Strip HTML tags
        var text = Regex.Replace(body, "<[^>]*>", " ");
        
        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Truncate
        if (text.Length <= MaxExcerptLength)
        {
            return text;
        }

        // Find word boundary
        var truncated = text[..MaxExcerptLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > MaxExcerptLength * 0.7)
        {
            truncated = truncated[..lastSpace];
        }

        return truncated + "...";
    }
}

