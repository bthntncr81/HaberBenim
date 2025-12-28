using Microsoft.Extensions.Options;

namespace HaberPlatform.Api.Services.Reporting;

/// <summary>
/// Background worker that generates daily reports at the end of each day
/// </summary>
public class DailyReportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReportsOptions _options;
    private readonly ILogger<DailyReportWorker> _logger;
    private readonly TimeZoneInfo _timeZone;

    public DailyReportWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReportsOptions> options,
        ILogger<DailyReportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyReportWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate next run time (00:05 local time next day)
                var nextRunTime = CalculateNextRunTime();
                var delayUntilNextRun = nextRunTime - DateTime.UtcNow;

                if (delayUntilNextRun > TimeSpan.Zero)
                {
                    _logger.LogInformation("DailyReportWorker sleeping until {NextRunTime} UTC", nextRunTime);
                    await Task.Delay(delayUntilNextRun, stoppingToken);
                }

                // Generate report for yesterday
                await GenerateDailyReportAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DailyReportWorker");
                // Wait 1 hour before retrying
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) { }
            }
        }

        _logger.LogInformation("DailyReportWorker stopped");
    }

    private DateTime CalculateNextRunTime()
    {
        // Get current local time
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _timeZone);

        // Calculate tomorrow at 00:05 local time
        var tomorrowLocal = nowLocal.Date.AddDays(1).AddMinutes(5);
        
        // Convert back to UTC
        return TimeZoneInfo.ConvertTimeToUtc(tomorrowLocal, _timeZone);
    }

    private async Task GenerateDailyReportAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<DailyReportService>();

        // Get yesterday's date in local timezone
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _timeZone);
        var yesterdayLocal = DateOnly.FromDateTime(nowLocal.Date.AddDays(-1));

        // Check if report already exists
        if (await reportService.ReportExistsAsync(yesterdayLocal, ct))
        {
            _logger.LogInformation("Daily report for {Date} already exists, skipping", yesterdayLocal);
            return;
        }

        _logger.LogInformation("Generating automatic daily report for {Date}", yesterdayLocal);

        var result = await reportService.GenerateDailyReportAsync(yesterdayLocal, null, ct);

        if (result.Success)
        {
            _logger.LogInformation("Automatic daily report generated: {FilePath}", result.FilePath);
        }
        else
        {
            _logger.LogError("Failed to generate automatic daily report: {Error}", result.Error);
        }
    }
}

