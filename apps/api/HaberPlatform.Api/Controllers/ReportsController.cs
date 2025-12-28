using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HaberPlatform.Api.Services.Reporting;
using System.Security.Claims;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly DailyReportService _reportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        DailyReportService reportService,
        ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Generate daily report for a specific date
    /// </summary>
    [HttpPost("daily/generate")]
    public async Task<ActionResult<DailyReportGenerateResponse>> GenerateDailyReport(
        [FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var reportDate))
        {
            return BadRequest(new { error = "Invalid date format. Use YYYY-MM-DD" });
        }

        var userId = GetCurrentUserId();
        var result = await _reportService.GenerateDailyReportAsync(reportDate, userId);

        if (!result.Success)
        {
            return StatusCode(500, new DailyReportGenerateResponse(
                false, reportDate.ToString("yyyy-MM-dd"), null, null, 0, result.Error));
        }

        return Ok(new DailyReportGenerateResponse(
            true, 
            reportDate.ToString("yyyy-MM-dd"), 
            result.FilePath,
            result.ReportRunId,
            result.ItemCount,
            null
        ));
    }

    /// <summary>
    /// Download daily report for a specific date
    /// </summary>
    [HttpGet("daily/download")]
    public async Task<ActionResult> DownloadDailyReport([FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var reportDate))
        {
            return BadRequest(new { error = "Invalid date format. Use YYYY-MM-DD" });
        }

        var filePath = await _reportService.GetReportFilePathAsync(reportDate);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = $"Report for {reportDate:yyyy-MM-dd} not found" });
        }

        var fileName = Path.GetFileName(filePath);
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

        return File(fileBytes, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
            fileName);
    }

    /// <summary>
    /// Get list of report runs
    /// </summary>
    [HttpGet("daily/runs")]
    public async Task<ActionResult<List<DailyReportRunDto>>> GetReportRuns(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        if (!string.IsNullOrEmpty(from) && DateOnly.TryParse(from, out var parsedFrom))
        {
            fromDate = parsedFrom;
        }

        if (!string.IsNullOrEmpty(to) && DateOnly.TryParse(to, out var parsedTo))
        {
            toDate = parsedTo;
        }

        var runs = await _reportService.GetReportRunsAsync(fromDate, toDate);
        return Ok(runs);
    }
}

public record DailyReportGenerateResponse(
    bool Success,
    string ReportDate,
    string? FilePath,
    Guid? ReportRunId,
    int ItemCount,
    string? Error
);

