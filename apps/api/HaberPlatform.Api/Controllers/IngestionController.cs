using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/ingestion")]
[Authorize(Roles = "Admin")]
public class IngestionController : ControllerBase
{
    private readonly RssIngestionService _ingestionService;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(
        RssIngestionService ingestionService,
        ILogger<IngestionController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    [HttpPost("rss/run-now")]
    public async Task<IActionResult> RunRssIngestion(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual RSS ingestion triggered by admin");

        var result = await _ingestionService.IngestAllAsync(cancellationToken);

        return Ok(new IngestionRunResponse(
            Ok: true,
            SourcesProcessed: result.SourcesProcessed,
            ItemsInserted: result.ItemsInserted,
            Duplicates: result.DuplicatesFound,
            Errors: result.Errors > 0 ? result.ErrorMessages : null,
            ByDecisionTypeCounts: result.ByDecisionTypeCounts.Count > 0 ? result.ByDecisionTypeCounts : null
        ));
    }
}
