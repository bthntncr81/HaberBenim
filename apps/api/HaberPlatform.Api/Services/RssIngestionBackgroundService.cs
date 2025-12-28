namespace HaberPlatform.Api.Services;

public class RssIngestionBackgroundService : BackgroundService
{
    private readonly RssIngestionService _ingestionService;
    private readonly ILogger<RssIngestionBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);

    public RssIngestionBackgroundService(
        RssIngestionService ingestionService,
        ILogger<RssIngestionBackgroundService> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RSS Ingestion Background Service started. Interval: {Interval}", _interval);

        // Wait a bit before first run to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled RSS ingestion...");
                var result = await _ingestionService.IngestAllAsync(stoppingToken);
                _logger.LogInformation(
                    "Scheduled RSS ingestion completed: {Sources} sources, {Items} items, {Duplicates} duplicates",
                    result.SourcesProcessed, result.ItemsInserted, result.DuplicatesFound);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled RSS ingestion");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("RSS Ingestion Background Service stopped");
    }
}


