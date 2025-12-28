using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(Roles = "Admin,Editor")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(AppDbContext db, ILogger<AnalyticsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get analytics overview for a date range
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewResponse>> GetOverview(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
        var to = toUtc ?? DateTime.UtcNow;

        // Total published content items in range
        var totalPublished = await _db.ContentItems
            .CountAsync(c => c.Status == ContentStatuses.Published
                && c.PublishedAtUtc >= from
                && c.PublishedAtUtc <= to);

        // By channel - count successful publish logs (distinct by contentId+versionNo+channel)
        var channelCounts = await _db.ChannelPublishLogs
            .Where(l => l.Status == ChannelPublishStatuses.Success
                && l.CreatedAtUtc >= from
                && l.CreatedAtUtc <= to)
            .GroupBy(l => l.Channel)
            .Select(g => new { Channel = g.Key, Count = g.Select(l => new { l.ContentItemId, l.VersionNo }).Distinct().Count() })
            .ToListAsync();

        var byChannel = new Dictionary<string, int>
        {
            { "Web", 0 },
            { "Mobile", 0 },
            { "X", 0 }
        };
        foreach (var cc in channelCounts)
        {
            if (byChannel.ContainsKey(cc.Channel))
            {
                byChannel[cc.Channel] = cc.Count;
            }
        }

        // Auto vs Editorial publish origin
        var originCounts = await _db.ContentItems
            .Where(c => c.Status == ContentStatuses.Published
                && c.PublishedAtUtc >= from
                && c.PublishedAtUtc <= to)
            .GroupBy(c => c.PublishOrigin ?? "Unknown")
            .Select(g => new { Origin = g.Key, Count = g.Count() })
            .ToListAsync();

        var autoVsEditorial = new Dictionary<string, int>
        {
            { "Auto", 0 },
            { "Editorial", 0 },
            { "Unknown", 0 }
        };
        foreach (var oc in originCounts)
        {
            if (autoVsEditorial.ContainsKey(oc.Origin))
            {
                autoVsEditorial[oc.Origin] = oc.Count;
            }
            else
            {
                autoVsEditorial["Unknown"] += oc.Count;
            }
        }

        // Top sources
        var topSources = await _db.ContentItems
            .Where(c => c.Status == ContentStatuses.Published
                && c.PublishedAtUtc >= from
                && c.PublishedAtUtc <= to)
            .GroupBy(c => c.Source.Name)
            .Select(g => new TopSourceDto(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .Take(10)
            .ToListAsync();

        // Additional metrics
        var totalIngested = await _db.ContentItems
            .CountAsync(c => c.IngestedAtUtc >= from && c.IngestedAtUtc <= to);

        var totalPending = await _db.ContentItems
            .CountAsync(c => c.Status == ContentStatuses.PendingApproval);

        var totalRejected = await _db.ContentItems
            .CountAsync(c => c.Status == ContentStatuses.Rejected
                && c.LastEditedAtUtc >= from
                && c.LastEditedAtUtc <= to);

        var totalCorrections = await _db.ContentRevisions
            .CountAsync(r => r.ActionType == RevisionActionTypes.Corrected
                && r.CreatedAtUtc >= from
                && r.CreatedAtUtc <= to);

        return Ok(new AnalyticsOverviewResponse(
            from,
            to,
            totalPublished,
            byChannel,
            autoVsEditorial,
            topSources,
            totalIngested,
            totalPending,
            totalRejected,
            totalCorrections
        ));
    }

    /// <summary>
    /// Get publishing trends over time
    /// </summary>
    [HttpGet("trends")]
    public async Task<ActionResult<List<DailyTrendDto>>> GetTrends(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
        var to = toUtc ?? DateTime.UtcNow;

        var trends = await _db.ContentItems
            .Where(c => c.Status == ContentStatuses.Published
                && c.PublishedAtUtc >= from
                && c.PublishedAtUtc <= to)
            .GroupBy(c => c.PublishedAtUtc.Date)
            .Select(g => new DailyTrendDto(
                DateOnly.FromDateTime(g.Key),
                g.Count()
            ))
            .OrderBy(t => t.Date)
            .ToListAsync();

        return Ok(trends);
    }
}

public record AnalyticsOverviewResponse(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalPublished,
    Dictionary<string, int> ByChannel,
    Dictionary<string, int> AutoVsEditorial,
    List<TopSourceDto> TopSources,
    int TotalIngested,
    int TotalPending,
    int TotalRejected,
    int TotalCorrections
);

public record TopSourceDto(string SourceName, int Count);

public record DailyTrendDto(DateOnly Date, int Count);

