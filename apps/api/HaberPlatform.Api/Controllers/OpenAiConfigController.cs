using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// OpenAI configuration endpoints for Admin
/// </summary>
[ApiController]
[Route("api/v1/integrations/openai")]
[Authorize(Roles = "Admin")]
public class OpenAiConfigController : ControllerBase
{
    private readonly OpenAiConfigService _openAiService;
    private readonly ILogger<OpenAiConfigController> _logger;

    public OpenAiConfigController(
        OpenAiConfigService openAiService,
        ILogger<OpenAiConfigController> logger)
    {
        _openAiService = openAiService;
        _logger = logger;
    }

    /// <summary>
    /// Get current OpenAI API key configuration status
    /// </summary>
    /// <remarks>
    /// Returns whether an API key is configured, last 4 characters of the key,
    /// and results of the last connection test.
    /// Never returns the full API key.
    /// </remarks>
    [HttpGet("status")]
    [ProducesResponseType(typeof(OpenAiKeyStatusResponse), 200)]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _openAiService.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Save OpenAI API key (encrypted at rest)
    /// </summary>
    /// <remarks>
    /// The API key is encrypted using ASP.NET Core DataProtection before storage.
    /// Previous test results are cleared when saving a new key.
    /// </remarks>
    [HttpPost("save")]
    [ProducesResponseType(typeof(SaveOpenAiKeyResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> SaveKey([FromBody] SaveOpenAiKeyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid request", details = ModelState });
        }

        // Additional validation
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "API key is required" });
        }

        // Check for obviously invalid key formats
        if (!request.ApiKey.StartsWith("sk-") && !request.ApiKey.StartsWith("sess-"))
        {
            _logger.LogWarning("Suspicious API key format - doesn't start with sk- or sess-");
            // Still allow saving, but log warning
        }

        var result = await _openAiService.SaveKeyAsync(request);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result);
    }

    /// <summary>
    /// Test the stored OpenAI API key
    /// </summary>
    /// <remarks>
    /// Calls OpenAI GET /v1/models to validate the API key.
    /// Stores test results including whether sora-2 model is available.
    /// </remarks>
    [HttpPost("test")]
    [ProducesResponseType(typeof(TestOpenAiResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> TestConnection()
    {
        var result = await _openAiService.TestConnectionAsync();

        if (!result.Success)
        {
            // Return 200 with error in response body (test ran but failed)
            return Ok(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete the stored OpenAI API key
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteKey()
    {
        await _openAiService.DeleteKeyAsync();
        return NoContent();
    }
}
