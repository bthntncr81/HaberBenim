using Microsoft.Extensions.Options;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.XIntegration;

/// <summary>
/// Background worker that polls X sources for new tweets
/// </summary>
public class XIngestionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<XIngestionWorker> _logger;
    private readonly XIngestionOptions _options;

    public XIngestionWorker(
        IServiceProvider serviceProvider,
        ILogger<XIngestionWorker> logger,
        IOptions<XIngestionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("X Ingestion Worker started. Polling interval: {Interval}s", 
            _options.PollingIntervalSeconds);

        // Initial delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollXSourcesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "X ingestion cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("X Ingestion Worker stopped");
    }

    private async Task PollXSourcesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<XIngestionService>();

        await ingestionService.PollAllSourcesAsync(ct);
    }
}

