using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ClosedXML.Excel;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Reporting;

/// <summary>
/// Service for generating daily XLSX reports
/// </summary>
public class DailyReportService
{
    private readonly AppDbContext _db;
    private readonly ReportsOptions _options;
    private readonly ILogger<DailyReportService> _logger;
    private readonly TimeZoneInfo _timeZone;

    public DailyReportService(
        AppDbContext db,
        IOptions<ReportsOptions> options,
        ILogger<DailyReportService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
        
        // Try to get timezone, fallback to UTC if not found
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
        }
        catch
        {
            _logger.LogWarning("TimeZone {TimeZoneId} not found, falling back to UTC", _options.TimeZoneId);
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Generate daily report for a specific date
    /// </summary>
    public async Task<DailyReportResult> GenerateDailyReportAsync(
        DateOnly dateLocal, 
        Guid? createdByUserId = null,
        CancellationToken ct = default)
    {
        var result = new DailyReportResult { ReportDate = dateLocal };

        try
        {
            // Calculate UTC range for the day in local timezone
            var dayStart = new DateTime(dateLocal.Year, dateLocal.Month, dateLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var dayEnd = dayStart.AddDays(1);
            
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStart, _timeZone);
            var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEnd, _timeZone);

            _logger.LogInformation("Generating daily report for {Date} (UTC range: {Start} to {End})",
                dateLocal, dayStartUtc, dayEndUtc);

            // Get successful publish logs in the date range, grouped by content+version
            var publishData = await GetPublishDataAsync(dayStartUtc, dayEndUtc, ct);

            if (publishData.Count == 0)
            {
                _logger.LogInformation("No published content found for {Date}", dateLocal);
            }

            // Ensure output directory exists
            var outputDir = Path.GetFullPath(_options.OutputDir);
            Directory.CreateDirectory(outputDir);

            // Generate filename
            var fileName = $"daily-report-{dateLocal:yyyy-MM-dd}.xlsx";
            var filePath = Path.Combine(outputDir, fileName);

            // Generate XLSX
            await GenerateXlsxAsync(filePath, dateLocal, publishData, ct);

            // Create report run record
            var reportRun = new DailyReportRun
            {
                Id = Guid.NewGuid(),
                ReportDateLocal = dateLocal,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = createdByUserId,
                FilePath = filePath,
                Status = DailyReportStatuses.Succeeded
            };
            _db.DailyReportRuns.Add(reportRun);
            await _db.SaveChangesAsync(ct);

            result.Success = true;
            result.FilePath = filePath;
            result.ReportRunId = reportRun.Id;
            result.ItemCount = publishData.Count;

            _logger.LogInformation("Daily report generated: {FilePath} with {Count} items", 
                filePath, publishData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily report for {Date}", dateLocal);
            
            // Record failed run
            var reportRun = new DailyReportRun
            {
                Id = Guid.NewGuid(),
                ReportDateLocal = dateLocal,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = createdByUserId,
                FilePath = string.Empty,
                Status = DailyReportStatuses.Failed,
                Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message
            };
            _db.DailyReportRuns.Add(reportRun);
            await _db.SaveChangesAsync(ct);

            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Check if a report already exists for a date
    /// </summary>
    public async Task<bool> ReportExistsAsync(DateOnly dateLocal, CancellationToken ct = default)
    {
        return await _db.DailyReportRuns
            .AnyAsync(r => r.ReportDateLocal == dateLocal 
                && r.Status == DailyReportStatuses.Succeeded, ct);
    }

    /// <summary>
    /// Get report file path for a date
    /// </summary>
    public async Task<string?> GetReportFilePathAsync(DateOnly dateLocal, CancellationToken ct = default)
    {
        var report = await _db.DailyReportRuns
            .Where(r => r.ReportDateLocal == dateLocal && r.Status == DailyReportStatuses.Succeeded)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return report?.FilePath;
    }

    /// <summary>
    /// Get report runs for a date range
    /// </summary>
    public async Task<List<DailyReportRunDto>> GetReportRunsAsync(
        DateOnly? from, 
        DateOnly? to, 
        CancellationToken ct = default)
    {
        var query = _db.DailyReportRuns
            .Include(r => r.CreatedByUser)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(r => r.ReportDateLocal >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.ReportDateLocal <= to.Value);

        return await query
            .OrderByDescending(r => r.ReportDateLocal)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Take(100)
            .Select(r => new DailyReportRunDto(
                r.Id,
                r.ReportDateLocal,
                r.CreatedAtUtc,
                r.CreatedByUser != null ? r.CreatedByUser.DisplayName : null,
                r.Status,
                r.Error
            ))
            .ToListAsync(ct);
    }

    private async Task<List<ReportItemData>> GetPublishDataAsync(
        DateTime fromUtc, 
        DateTime toUtc, 
        CancellationToken ct)
    {
        // Get all successful channel logs in the date range
        var logs = await _db.ChannelPublishLogs
            .Include(l => l.ContentItem)
                .ThenInclude(c => c.Source)
            .Include(l => l.ContentItem)
                .ThenInclude(c => c.Draft)
            .Include(l => l.ContentItem)
                .ThenInclude(c => c.PublishedByUser)
            .Where(l => l.Status == ChannelPublishStatuses.Success 
                && l.CreatedAtUtc >= fromUtc 
                && l.CreatedAtUtc < toUtc)
            .ToListAsync(ct);

        // Group by content item + version to get unique publish events
        var grouped = logs
            .GroupBy(l => new { l.ContentItemId, l.VersionNo })
            .Select(g => new ReportItemData
            {
                PublishedAtUtc = g.Min(l => l.CreatedAtUtc),
                Title = g.First().ContentItem.Draft?.WebTitle ?? g.First().ContentItem.Title,
                CategoryOrGroup = g.First().ContentItem.Source?.Group,
                SourceName = g.First().ContentItem.Source?.Name ?? "Unknown",
                PublishOrigin = g.First().ContentItem.PublishOrigin ?? "Unknown",
                Channels = string.Join(", ", g.Select(l => l.Channel).Distinct().OrderBy(c => c)),
                PublishedBy = g.First().ContentItem.PublishedByUser?.DisplayName ?? "System",
                VersionNo = g.Key.VersionNo
            })
            .OrderBy(r => r.PublishedAtUtc)
            .ToList();

        return grouped;
    }

    private Task GenerateXlsxAsync(
        string filePath, 
        DateOnly reportDate,
        List<ReportItemData> data, 
        CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"Report {reportDate:yyyy-MM-dd}");

        // Headers
        var headers = new[] { "Published At", "Title", "Category/Group", "Source", "Origin", "Channels", "Published By", "Version" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Data rows
        for (int row = 0; row < data.Count; row++)
        {
            var item = data[row];
            var excelRow = row + 2;

            // Convert UTC to local time for display
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(item.PublishedAtUtc, _timeZone);
            
            worksheet.Cell(excelRow, 1).Value = localTime.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(excelRow, 2).Value = item.Title ?? "";
            worksheet.Cell(excelRow, 3).Value = item.CategoryOrGroup ?? "";
            worksheet.Cell(excelRow, 4).Value = item.SourceName;
            worksheet.Cell(excelRow, 5).Value = item.PublishOrigin;
            worksheet.Cell(excelRow, 6).Value = item.Channels;
            worksheet.Cell(excelRow, 7).Value = item.PublishedBy;
            worksheet.Cell(excelRow, 8).Value = item.VersionNo;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add summary row
        var summaryRow = data.Count + 3;
        worksheet.Cell(summaryRow, 1).Value = "Total Items:";
        worksheet.Cell(summaryRow, 2).Value = data.Count;
        worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;

        workbook.SaveAs(filePath);
        
        return Task.CompletedTask;
    }
}

public class ReportItemData
{
    public DateTime PublishedAtUtc { get; set; }
    public string? Title { get; set; }
    public string? CategoryOrGroup { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string PublishOrigin { get; set; } = string.Empty;
    public string Channels { get; set; } = string.Empty;
    public string PublishedBy { get; set; } = string.Empty;
    public int VersionNo { get; set; }
}

public class DailyReportResult
{
    public bool Success { get; set; }
    public DateOnly ReportDate { get; set; }
    public string? FilePath { get; set; }
    public Guid? ReportRunId { get; set; }
    public int ItemCount { get; set; }
    public string? Error { get; set; }
}

public record DailyReportRunDto(
    Guid Id,
    DateOnly ReportDateLocal,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    string Status,
    string? Error
);

