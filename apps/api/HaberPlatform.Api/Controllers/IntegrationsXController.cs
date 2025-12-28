using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.XIntegration;

namespace HaberPlatform.Api.Controllers;

/// <summary>
/// X (Twitter) OAuth2 and OAuth1.0a integration endpoints
/// </summary>
[ApiController]
[Route("api/v1/integrations/x")]
public class IntegrationsXController : ControllerBase
{
    private readonly XOAuthService _oauthService;
    private readonly XApiClient _apiClient;
    private readonly XIngestionService _ingestionService;
    private readonly AppDbContext _db;
    private readonly ILogger<IntegrationsXController> _logger;

    public IntegrationsXController(
        XOAuthService oauthService,
        XApiClient apiClient,
        XIngestionService ingestionService,
        AppDbContext db,
        ILogger<IntegrationsXController> logger)
    {
        _oauthService = oauthService;
        _apiClient = apiClient;
        _ingestionService = ingestionService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Start OAuth2 PKCE flow - returns authorization URL
    /// </summary>
    [HttpGet("connect")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ConnectResponse>> Connect(
        [FromQuery] string scopes = "tweet.read tweet.write users.read offline.access")
    {
        try
        {
            // Generate PKCE values
            var (codeVerifier, codeChallenge) = _oauthService.GeneratePkce();
            var state = _oauthService.GenerateState();

            // Store state and code_verifier server-side
            await _oauthService.StoreOAuthStateAsync(state, codeVerifier);

            // Build authorization URL
            var authorizeUrl = _oauthService.BuildAuthorizeUrl(codeChallenge, state, scopes);

            _logger.LogInformation("Generated OAuth2 connect URL for X");

            return Ok(new ConnectResponse(authorizeUrl, state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate X connect URL");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// OAuth2 callback - exchanges code for tokens and stores connection
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous] // Callback from X, no JWT
    public async Task<ActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        try
        {
            // Handle error from X
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("X OAuth error: {Error} - {Description}", error, error_description);
                return Content(GenerateCallbackHtml(false, $"Authorization denied: {error}"), "text/html");
            }

            // Validate required params
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return Content(GenerateCallbackHtml(false, "Missing code or state parameter"), "text/html");
            }

            // Validate and consume state
            var oauthState = await _oauthService.ValidateAndConsumeStateAsync(state);
            if (oauthState == null)
            {
                _logger.LogWarning("Invalid or expired OAuth state: {State}", state);
                return Content(GenerateCallbackHtml(false, "Invalid or expired state. Please try again."), "text/html");
            }

            // Exchange code for tokens
            var tokens = await _oauthService.ExchangeCodeForTokenAsync(code, oauthState.CodeVerifier);

            // Get user info
            var user = await _oauthService.GetCurrentUserAsync(tokens.AccessToken!);

            // Create or update connection
            var connection = await _oauthService.CreateOrUpdateConnectionAsync(tokens, user);

            _logger.LogInformation("Successfully connected X account @{Username} (ID: {UserId})", 
                user.Username, user.Id);

            return Content(GenerateCallbackHtml(true, $"Successfully connected @{user.Username}"), "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "X OAuth callback failed");
            return Content(GenerateCallbackHtml(false, $"Connection failed: {ex.Message}"), "text/html");
        }
    }

    /// <summary>
    /// Get all X connections
    /// </summary>
    [HttpGet("status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ConnectionListResponse>> GetStatus()
    {
        try
        {
            var connections = await _oauthService.GetConnectionsAsync();

            var dtos = connections.Select(c => new ConnectionStatusDto(
                c.Id,
                c.Name,
                c.XUsername,
                c.XUserId,
                c.ScopesCsv,
                c.AccessTokenExpiresAtUtc,
                c.IsDefaultPublisher,
                c.IsActive,
                c.CreatedAtUtc
            )).ToList();

            return Ok(new ConnectionListResponse(dtos, dtos.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X connection status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set a connection as the default publisher
    /// </summary>
    [HttpPost("set-default/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetDefaultPublisher(Guid id, [FromBody] SetDefaultRequest? request)
    {
        try
        {
            var success = await _oauthService.SetDefaultPublisherAsync(id);
            if (!success)
            {
                return NotFound(new { error = "Connection not found" });
            }

            _logger.LogInformation("Set X connection {ConnectionId} as default publisher", id);
            return Ok(new { message = "Default publisher updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default X publisher");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Disconnect (deactivate) a connection
    /// </summary>
    [HttpPost("disconnect/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Disconnect(Guid id)
    {
        try
        {
            var success = await _oauthService.DisconnectAsync(id);
            if (!success)
            {
                return NotFound(new { error = "Connection not found" });
            }

            _logger.LogInformation("Disconnected X connection {ConnectionId}", id);
            return Ok(new { message = "Connection disconnected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect X connection");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test the default publisher connection
    /// </summary>
    [HttpGet("test")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> TestConnection()
    {
        try
        {
            var result = await _oauthService.GetDefaultPublisherAsync();
            if (result == null)
            {
                return Ok(new { 
                    connected = false, 
                    message = "No default publisher configured" 
                });
            }

            var (connection, accessToken) = result.Value;

            // Try to get user info with the token
            var user = await _oauthService.GetCurrentUserAsync(accessToken);

            return Ok(new
            {
                connected = true,
                username = user.Username,
                userId = user.Id,
                tokenExpiresAt = connection.AccessTokenExpiresAtUtc,
                message = "Connection is healthy"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "X connection test failed");
            return Ok(new
            {
                connected = false,
                error = ex.Message,
                message = "Connection test failed - may need to reconnect"
            });
        }
    }

    // ================================
    // OAuth 1.0a Test Endpoints
    // ================================

    /// <summary>
    /// Test OAuth 1.0a credentials by getting the authenticated user info
    /// </summary>
    [HttpGet("test-oauth1")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> TestOAuth1Credentials()
    {
        try
        {
            var credentials = _oauthService.GetOAuth1Credentials();
            if (credentials == null)
            {
                return Ok(new
                {
                    success = false,
                    configured = false,
                    message = "OAuth 1.0a credentials are not fully configured. Please set API Key, API Secret Key, Access Token, and Access Token Secret."
                });
            }

            _logger.LogInformation("Testing OAuth 1.0a credentials...");

            // Try to get authenticated user info
            var result = await _apiClient.GetMeOAuth1Async(credentials);

            if (result.IsAuthError)
            {
                _logger.LogWarning("OAuth 1.0a test failed - auth error: {Error}", result.Error);
                return Ok(new
                {
                    success = false,
                    configured = true,
                    message = "Authentication failed. Please verify your credentials.",
                    error = result.Error
                });
            }

            if (!result.IsSuccess || result.Data?.Data == null)
            {
                _logger.LogWarning("OAuth 1.0a test failed: {Error}", result.Error);
                return Ok(new
                {
                    success = false,
                    configured = true,
                    message = result.Error ?? "Failed to get user info",
                    statusCode = result.StatusCode
                });
            }

            var user = result.Data.Data;
            _logger.LogInformation("OAuth 1.0a test successful. Connected as @{Username}", user.Username);

            return Ok(new
            {
                success = true,
                configured = true,
                message = $"Successfully connected as @{user.Username}",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    username = user.Username
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth 1.0a test failed with exception");
            return Ok(new
            {
                success = false,
                error = ex.Message,
                message = "Test failed with an exception"
            });
        }
    }

    /// <summary>
    /// Test Bearer Token (App-only auth) by calling a basic endpoint
    /// </summary>
    [HttpGet("test-bearer")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> TestBearerToken([FromQuery] string? username = "twitter")
    {
        try
        {
            var bearerToken = _oauthService.GetAppBearerToken();
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                return Ok(new
                {
                    success = false,
                    configured = false,
                    message = "Bearer Token is not configured. Please save X_APP_BEARER_TOKEN."
                });
            }

            var u = (username ?? "twitter").Trim().TrimStart('@');
            if (string.IsNullOrWhiteSpace(u) || u.Contains(' '))
            {
                return BadRequest(new { error = "Invalid username. Example: twitter" });
            }

            _logger.LogInformation("Testing Bearer Token with /2/users/by/username/{Username}", u);

            // Use a basic endpoint that typically works across access tiers
            var result = await _apiClient.GetUserByUsernameAsync(u, bearerToken, isAppToken: true);

            if (result.IsAuthError)
            {
                _logger.LogWarning("Bearer Token test failed - auth error: {Error}", result.Error);
                return Ok(new
                {
                    success = false,
                    configured = true,
                    message = "Authentication failed. Please verify your Bearer Token.",
                    error = result.Error
                });
            }

            if (result.IsRateLimited)
            {
                return Ok(new
                {
                    success = false,
                    configured = true,
                    message = "Rate limited. Please try again later.",
                    resetAt = result.RateLimitResetAt
                });
            }

            if (!result.IsSuccess)
            {
                return Ok(new
                {
                    success = false,
                    configured = true,
                    message = result.Error ?? "Request failed",
                    statusCode = result.StatusCode
                });
            }

            var user = result.Data?.Data;
            _logger.LogInformation("Bearer Token test successful. User resolved: @{Username} ({Id})", user?.Username, user?.Id);

            return Ok(new
            {
                success = true,
                configured = true,
                message = $"Bearer Token is working. Resolved @{user?.Username}.",
                user = new
                {
                    id = user?.Id,
                    name = user?.Name,
                    username = user?.Username
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bearer Token test failed with exception");
            return Ok(new
            {
                success = false,
                error = ex.Message,
                message = "Test failed with an exception"
            });
        }
    }

    /// <summary>
    /// Get current configuration status for X integration
    /// </summary>
    [HttpGet("config-status")]
    [Authorize(Roles = "Admin")]
    public ActionResult GetConfigStatus()
    {
        var hasOAuth1 = _oauthService.HasOAuth1Credentials();
        var bearer = _oauthService.GetAppBearerToken();
        var hasBearerToken = !string.IsNullOrWhiteSpace(bearer);
        var hasOAuth2ClientId = true;
        
        try
        {
            _oauthService.GetClientId();
        }
        catch
        {
            hasOAuth2ClientId = false;
        }

        return Ok(new
        {
            oauth1 = new
            {
                configured = hasOAuth1,
                hasApiKey = !string.IsNullOrWhiteSpace(_oauthService.GetApiKey()),
                hasApiSecretKey = !string.IsNullOrWhiteSpace(_oauthService.GetApiSecretKey()),
                hasAccessToken = !string.IsNullOrWhiteSpace(_oauthService.GetAccessToken()),
                hasAccessTokenSecret = !string.IsNullOrWhiteSpace(_oauthService.GetAccessTokenSecret()),
                description = "OAuth 1.0a is used for posting tweets and user-context actions"
            },
            oauth2 = new
            {
                configured = hasOAuth2ClientId,
                hasBearerToken = hasBearerToken,
                // Debug info is masked (no secrets). Helps verify the app is reading the token you saved.
                bearerDebug = new
                {
                    length = bearer?.Length ?? 0,
                    hasWhitespace = bearer != null && bearer.Any(char.IsWhiteSpace),
                    containsPercent = bearer != null && bearer.Contains('%'),
                    fingerprint = bearer == null || bearer.Length < 8
                        ? null
                        : $"{bearer[..4]}…{bearer[^4..]}"
                },
                description = "OAuth 2.0 Bearer Token is used for reading public data"
            },
            recommendations = new List<string>
            {
                hasOAuth1 ? "✓ OAuth 1.0a is configured - you can post tweets" : "⚠ Configure OAuth 1.0a credentials to post tweets",
                hasBearerToken ? "✓ Bearer Token is configured - you can read public data" : "⚠ Configure Bearer Token to read public tweets"
            }
        });
    }

    /// <summary>
    /// Post a test tweet using OAuth 1.0a credentials
    /// </summary>
    [HttpPost("test-post")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> TestPost([FromBody] TestPostRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Tweet text is required" });
            }

            if (request.Text.Length > 280)
            {
                return BadRequest(new { error = "Tweet text cannot exceed 280 characters" });
            }

            var credentials = _oauthService.GetOAuth1Credentials();
            if (credentials == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "OAuth 1.0a credentials are not configured"
                });
            }

            _logger.LogInformation("Posting test tweet: {Text}", request.Text);

            var result = await _apiClient.PostTweetOAuth1Async(credentials, request.Text);

            if (result.IsAuthError)
            {
                return Ok(new
                {
                    success = false,
                    message = "Authentication failed. Please verify your OAuth 1.0a credentials.",
                    error = result.Error,
                    details = result.RawJson
                });
            }

            if (result.IsRateLimited)
            {
                return Ok(new
                {
                    success = false,
                    message = "Rate limited. Please try again later.",
                    resetAt = result.RateLimitResetAt
                });
            }

            if (!result.IsSuccess || result.Data?.Data == null)
            {
                return Ok(new
                {
                    success = false,
                    message = result.Error ?? "Failed to post tweet",
                    statusCode = result.StatusCode
                });
            }

            var tweetData = result.Data.Data;
            _logger.LogInformation("Test tweet posted successfully. Tweet ID: {TweetId}", tweetData.Id);

            return Ok(new
            {
                success = true,
                message = "Tweet posted successfully!",
                tweet = new
                {
                    id = tweetData.Id,
                    text = tweetData.Text,
                    url = $"https://x.com/i/status/{tweetData.Id}"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test post failed with exception");
            return Ok(new
            {
                success = false,
                error = ex.Message,
                message = "Post failed with an exception"
            });
        }
    }

    #region Helpers

    private static string GenerateCallbackHtml(bool success, string message)
    {
        var color = success ? "#00ba7c" : "#f4212e";
        var icon = success ? "✓" : "✕";
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>X Connection</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #0f1419;
            color: #e7e9ea;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
        }}
        .container {{
            text-align: center;
            padding: 40px;
            background: #16181c;
            border-radius: 16px;
            border: 1px solid #2f3336;
            max-width: 400px;
        }}
        .icon {{
            font-size: 48px;
            color: {color};
            margin-bottom: 16px;
        }}
        .message {{
            font-size: 18px;
            margin-bottom: 24px;
        }}
        .close-hint {{
            color: #71767b;
            font-size: 14px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>{icon}</div>
        <div class='message'>{message}</div>
        <div class='close-hint'>You can close this window.</div>
    </div>
    <script>
        setTimeout(function() {{ window.close(); }}, 3000);
    </script>
</body>
</html>";
    }

    #endregion

    #region Ingestion Endpoints

    /// <summary>
    /// Get ingestion status for all X sources
    /// </summary>
    [HttpGet("ingestion-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetIngestionStatus()
    {
        try
        {
            var xSources = await _db.Sources
                .Where(s => s.Type == "X" && s.IsActive)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Identifier,
                    s.IsActive
                })
                .ToListAsync();

            var sourceIds = xSources.Select(s => s.Id).ToList();
            
            var states = await _db.XSourceStates
                .Where(s => sourceIds.Contains(s.SourceId))
                .ToDictionaryAsync(s => s.SourceId);

            var contentCounts = await _db.ContentItems
                .Where(c => sourceIds.Contains(c.SourceId))
                .GroupBy(c => c.SourceId)
                .Select(g => new { SourceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SourceId, x => x.Count);

            var result = xSources.Select(s => {
                states.TryGetValue(s.Id, out var state);
                contentCounts.TryGetValue(s.Id, out var count);
                
                return new
                {
                    sourceId = s.Id,
                    sourceName = s.Name,
                    identifier = s.Identifier,
                    isActive = s.IsActive,
                    xUserId = state?.XUserId,
                    lastSinceId = state?.LastSinceId,
                    lastPolledAt = state?.LastPolledAtUtc,
                    lastSuccessAt = state?.LastSuccessAtUtc,
                    lastFailureAt = state?.LastFailureAtUtc,
                    lastError = state?.LastError,
                    consecutiveFailures = state?.ConsecutiveFailures ?? 0,
                    contentItemCount = count
                };
            });

            var totalContentItems = await _db.ContentItems.CountAsync();
            var xContentItems = contentCounts.Values.Sum();
            
            var hasCredentials = !string.IsNullOrEmpty(_oauthService.GetAppBearerToken()) 
                || _oauthService.HasOAuth1Credentials();

            return Ok(new
            {
                hasCredentials,
                credentialType = !string.IsNullOrEmpty(_oauthService.GetAppBearerToken()) 
                    ? "BearerToken" 
                    : (_oauthService.HasOAuth1Credentials() ? "OAuth1" : "None"),
                totalXSources = xSources.Count,
                totalContentItems,
                xContentItems,
                sources = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ingestion status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger ingestion for all X sources
    /// </summary>
    [HttpPost("trigger-ingestion")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> TriggerIngestion()
    {
        try
        {
            _logger.LogInformation("Manual X ingestion triggered");
            
            await _ingestionService.PollAllSourcesAsync();
            
            // Get updated counts
            var xSources = await _db.Sources
                .Where(s => s.Type == "X" && s.IsActive)
                .CountAsync();
                
            var xContentItems = await _db.ContentItems
                .Join(_db.Sources.Where(s => s.Type == "X"), 
                    c => c.SourceId, 
                    s => s.Id, 
                    (c, s) => c)
                .CountAsync();

            return Ok(new
            {
                success = true,
                message = "Ingestion completed",
                sourcesPolled = xSources,
                totalXContentItems = xContentItems
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual ingestion failed");
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message 
            });
        }
    }

    /// <summary>
    /// Manually trigger ingestion for a specific X source
    /// </summary>
    [HttpPost("trigger-ingestion/{sourceId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> TriggerSourceIngestion(Guid sourceId)
    {
        try
        {
            var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId);
            if (source == null)
                return NotFound(new { error = "Source not found" });

            if (source.Type != "X")
                return BadRequest(new { error = "Source is not an X source" });

            var bearerToken = _oauthService.GetAppBearerToken();
            var oauth1Credentials = _oauthService.GetOAuth1Credentials();

            if (string.IsNullOrEmpty(bearerToken) && oauth1Credentials == null)
                return BadRequest(new { error = "No X credentials configured" });

            _logger.LogInformation("Manual X ingestion triggered for source {SourceId}", sourceId);
            
            await _ingestionService.PollSourceAsync(source, bearerToken, oauth1Credentials);
            
            // Get updated state
            var state = await _db.XSourceStates.FirstOrDefaultAsync(s => s.SourceId == sourceId);
            var contentCount = await _db.ContentItems.CountAsync(c => c.SourceId == sourceId);

            return Ok(new
            {
                success = true,
                message = "Ingestion completed for source",
                sourceId,
                sourceName = source.Name,
                xUserId = state?.XUserId,
                lastSinceId = state?.LastSinceId,
                lastSuccessAt = state?.LastSuccessAtUtc,
                contentItemCount = contentCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual ingestion failed for source {SourceId}", sourceId);
            return StatusCode(500, new { 
                success = false, 
                sourceId,
                error = ex.Message 
            });
        }
    }

    #endregion
}

