using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Instagram;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// Instagram/Meta Graph API integration endpoints
/// Only Professional Instagram accounts connected to a Facebook Page are supported
/// </summary>
[ApiController]
[Route("api/v1/integrations/instagram")]
[Authorize(Roles = "Admin")]
public class IntegrationsInstagramController : ControllerBase
{
    private readonly InstagramOAuthService _oauthService;
    private readonly ILogger<IntegrationsInstagramController> _logger;

    public IntegrationsInstagramController(
        InstagramOAuthService oauthService,
        ILogger<IntegrationsInstagramController> logger)
    {
        _oauthService = oauthService;
        _logger = logger;
    }

    /// <summary>
    /// Start OAuth flow - returns authorization URL
    /// </summary>
    /// <remarks>
    /// Frontend should open the authorizeUrl in a popup or redirect.
    /// After user authorizes, Facebook redirects to your callback with a code.
    /// Then call POST /exchange with the code.
    /// </remarks>
    [HttpGet("connect")]
    public async Task<ActionResult<InstagramConnectResponse>> Connect()
    {
        try
        {
            var result = await _oauthService.GenerateConnectUrlAsync();
            if (result == null)
            {
                return BadRequest(new { 
                    error = "Meta App configuration missing",
                    details = "Please configure META_APP_ID and META_REDIRECT_URI in System Settings"
                });
            }

            _logger.LogInformation("Generated Instagram/Meta connect URL");
            return Ok(new InstagramConnectResponse(result.Value.authorizeUrl, result.Value.state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Instagram connect URL");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Exchange OAuth code for tokens and list available pages
    /// </summary>
    /// <remarks>
    /// After user authorizes, call this with the code received from Facebook.
    /// Returns list of Facebook Pages the user manages, with info about which have Instagram accounts.
    /// Then call POST /complete with the selected page.
    /// </remarks>
    [HttpPost("exchange")]
    public async Task<ActionResult<InstagramExchangeResponse>> Exchange(
        [FromBody] InstagramExchangeRequest request)
    {
        try
        {
            // Validate state
            var oauthState = await _oauthService.ValidateAndConsumeStateAsync(request.State);
            if (oauthState == null)
            {
                return BadRequest(new { error = "Invalid or expired state. Please start the connection flow again." });
            }

            var result = await _oauthService.ExchangeCodeAsync(
                request.Code,
                request.RedirectUri,
                request.State);

            if (result == null)
            {
                return BadRequest(new { error = "Failed to exchange code. Please try again." });
            }

            _logger.LogInformation("Exchanged Instagram OAuth code, found {PageCount} pages", result.Pages.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Instagram OAuth code");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Complete the connection by selecting a page with Instagram
    /// </summary>
    /// <remarks>
    /// Call after /exchange, specifying which page to use.
    /// The page must have a connected Professional Instagram account.
    /// </remarks>
    [HttpPost("complete")]
    public async Task<ActionResult<InstagramCompleteResponse>> Complete(
        [FromBody] InstagramCompleteRequest request)
    {
        try
        {
            var connection = await _oauthService.CompleteConnectionAsync(
                request.Name,
                request.PageId,
                request.State);

            if (connection == null)
            {
                return BadRequest(new InstagramCompleteResponse(
                    Success: false,
                    Message: "Failed to complete connection. The selected page may not have a Professional Instagram account connected.",
                    ConnectionId: null
                ));
            }

            _logger.LogInformation("Completed Instagram connection for @{Username}", connection.IgUsername);

            return Ok(new InstagramCompleteResponse(
                Success: true,
                Message: $"Successfully connected @{connection.IgUsername}",
                ConnectionId: connection.Id
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Instagram connection");
            return StatusCode(500, new InstagramCompleteResponse(
                Success: false,
                Message: ex.Message,
                ConnectionId: null
            ));
        }
    }

    /// <summary>
    /// Get all Instagram connections
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<InstagramConnectionListResponse>> GetStatus()
    {
        try
        {
            var connections = await _oauthService.GetConnectionsAsync();

            var dtos = connections.Select(c => new InstagramConnectionDto(
                Id: c.Id,
                Name: c.Name,
                PageId: c.PageId,
                PageName: c.PageName,
                IgUserId: c.IgUserId,
                IgUsername: c.IgUsername,
                ScopesCsv: c.ScopesCsv,
                TokenExpiresAtUtc: c.TokenExpiresAtUtc,
                IsDefaultPublisher: c.IsDefaultPublisher,
                IsActive: c.IsActive,
                CreatedAtUtc: c.CreatedAtUtc
            )).ToList();

            return Ok(new InstagramConnectionListResponse(dtos, dtos.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Instagram connection status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set a connection as the default publisher
    /// </summary>
    [HttpPost("set-default/{id:guid}")]
    public async Task<ActionResult> SetDefault(Guid id)
    {
        try
        {
            var success = await _oauthService.SetDefaultAsync(id);
            if (!success)
            {
                return NotFound(new { error = "Connection not found" });
            }

            _logger.LogInformation("Set Instagram connection {Id} as default", id);
            return Ok(new { message = "Default publisher updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Instagram default connection");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Disconnect (deactivate) a connection
    /// </summary>
    [HttpPost("disconnect/{id:guid}")]
    public async Task<ActionResult> Disconnect(Guid id)
    {
        try
        {
            var success = await _oauthService.DisconnectAsync(id);
            if (!success)
            {
                return NotFound(new { error = "Connection not found" });
            }

            _logger.LogInformation("Disconnected Instagram connection {Id}", id);
            return Ok(new { message = "Connection disconnected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect Instagram connection");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check configuration status
    /// </summary>
    [HttpGet("config-status")]
    public async Task<ActionResult> GetConfigStatus()
    {
        try
        {
            var appId = await _oauthService.GetAppIdAsync();
            var redirectUri = await _oauthService.GetRedirectUriAsync();
            var publicAssetUrl = await _oauthService.GetPublicAssetBaseUrlAsync();
            var connections = await _oauthService.GetConnectionsAsync();
            var defaultConnection = connections.FirstOrDefault(c => c.IsDefaultPublisher && c.IsActive);

            return Ok(new
            {
                hasAppId = !string.IsNullOrEmpty(appId),
                hasRedirectUri = !string.IsNullOrEmpty(redirectUri),
                hasPublicAssetUrl = !string.IsNullOrEmpty(publicAssetUrl),
                publicAssetUrlConfigured = !string.IsNullOrEmpty(publicAssetUrl),
                connectionCount = connections.Count,
                activeConnectionCount = connections.Count(c => c.IsActive),
                defaultConnection = defaultConnection != null ? new
                {
                    id = defaultConnection.Id,
                    name = defaultConnection.Name,
                    username = defaultConnection.IgUsername,
                    expiresAt = defaultConnection.TokenExpiresAtUtc,
                    isExpired = defaultConnection.TokenExpiresAtUtc < DateTime.UtcNow
                } : null,
                warnings = GetConfigWarnings(appId, redirectUri, publicAssetUrl, defaultConnection)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Instagram config status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manually add Instagram connection with token from Developer Console
    /// </summary>
    /// <remarks>
    /// Use this when OAuth flow doesn't work. Get the token from:
    /// Facebook Developer Console → Your App → Instagram → API setup → Generate token
    /// </remarks>
    [HttpPost("add-manual")]
    public async Task<ActionResult<InstagramCompleteResponse>> AddManual(
        [FromBody] InstagramManualAddRequest request)
    {
        try
        {
            var connection = await _oauthService.AddManualConnectionAsync(
                request.Name,
                request.IgUserId,
                request.IgUsername,
                request.AccessToken,
                request.ExpiresInDays ?? 60);

            if (connection == null)
            {
                return BadRequest(new InstagramCompleteResponse(
                    Success: false,
                    Message: "Failed to add connection. Please verify the token and Instagram User ID.",
                    ConnectionId: null
                ));
            }

            _logger.LogInformation("Manually added Instagram connection for @{Username}", connection.IgUsername);

            return Ok(new InstagramCompleteResponse(
                Success: true,
                Message: $"Successfully connected @{connection.IgUsername}",
                ConnectionId: connection.Id
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add manual Instagram connection");
            return StatusCode(500, new InstagramCompleteResponse(
                Success: false,
                Message: ex.Message,
                ConnectionId: null
            ));
        }
    }

    private List<string> GetConfigWarnings(string? appId, string? redirectUri, string? publicAssetUrl, Entities.InstagramConnection? defaultConnection)
    {
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(appId))
            warnings.Add("META_APP_ID not configured - required for Instagram OAuth");

        if (string.IsNullOrEmpty(redirectUri))
            warnings.Add("META_REDIRECT_URI not configured - required for Instagram OAuth");

        if (string.IsNullOrEmpty(publicAssetUrl))
            warnings.Add("PUBLIC_ASSET_BASE_URL not configured - required for Instagram image publishing (must be publicly accessible HTTPS URL)");

        if (defaultConnection == null)
            warnings.Add("No active Instagram connection - connect via Settings");
        else if (defaultConnection.TokenExpiresAtUtc < DateTime.UtcNow)
            warnings.Add($"Instagram token expired at {defaultConnection.TokenExpiresAtUtc:u} - reconnect needed");
        else if (defaultConnection.TokenExpiresAtUtc < DateTime.UtcNow.AddDays(7))
            warnings.Add($"Instagram token expires soon ({defaultConnection.TokenExpiresAtUtc:u}) - consider reconnecting");

        return warnings;
    }
}

public record InstagramManualAddRequest(
    string Name,
    string IgUserId,
    string? IgUsername,
    string AccessToken,
    int? ExpiresInDays
);

