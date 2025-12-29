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

        // By channel - count successful publish logs (distinct by contentId+versionNo per channel)
        // NOTE: Avoid EF generating invalid COUNT(DISTINCT *) SQL on PostgreSQL.
        var channelCounts = await _db.ChannelPublishLogs
            .Where(l => l.Status == ChannelPublishStatuses.Success
                && l.CreatedAtUtc >= from
                && l.CreatedAtUtc <= to)
            .GroupBy(l => new { l.Channel, l.ContentItemId, l.VersionNo })
            .Select(g => g.Key.Channel)
            .GroupBy(channel => channel)
            .Select(g => new { Channel = g.Key, Count = g.Count() })
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
        // NOTE: Use an anonymous projection for ordering; some EF/Npgsql combos struggle ordering by record properties.
        var topSourcesRaw = await _db.ContentItems
            .Where(c => c.Status == ContentStatuses.Published
                && c.PublishedAtUtc >= from
                && c.PublishedAtUtc <= to)
            .Join(
                _db.Sources,
                c => c.SourceId,
                s => s.Id,
                (c, s) => new { SourceName = s.Name }
            )
            .GroupBy(x => x.SourceName)
            .Select(g => new { SourceName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var topSources = topSourcesRaw
            .Select(x => new TopSourceDto(x.SourceName, x.Count))
            .ToList();

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

        var trendRaw = await _db.ContentItems
            .Where(c => c.Status == ContentStatuses.Published
                && c.PublishedAtUtc >= from
                && c.PublishedAtUtc <= to)
            .GroupBy(c => c.PublishedAtUtc.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(t => t.Date)
            .ToListAsync();

        var trends = trendRaw
            .Select(t => new DailyTrendDto(DateOnly.FromDateTime(t.Date), t.Count))
            .ToList();

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

