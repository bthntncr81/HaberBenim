using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Instagram;

/// <summary>
/// OAuth service for Instagram/Meta Graph API
/// </summary>
public class InstagramOAuthService
{
    private readonly AppDbContext _db;
    private readonly InstagramApiClient _apiClient;
    private readonly IDataProtector _protector;
    private readonly ILogger<InstagramOAuthService> _logger;

    // System setting keys
    private const string META_APP_ID = "META_APP_ID";
    private const string META_APP_SECRET = "META_APP_SECRET";
    private const string META_REDIRECT_URI = "META_REDIRECT_URI";
    private const string META_GRAPH_VERSION = "META_GRAPH_VERSION";
    private const string PUBLIC_ASSET_BASE_URL = "PUBLIC_ASSET_BASE_URL";

    // Default scopes required for Instagram publishing
    // pages_show_list: Required to list pages the user manages
    // pages_read_engagement: Required for page insights  
    // instagram_basic: Required for Instagram account info
    // instagram_content_publish: Required to publish to Instagram
    private const string DEFAULT_SCOPES = "public_profile,pages_show_list,pages_read_engagement,instagram_basic,instagram_content_publish";

    public InstagramOAuthService(
        AppDbContext db,
        InstagramApiClient apiClient,
        IDataProtectionProvider dataProtection,
        ILogger<InstagramOAuthService> logger)
    {
        _db = db;
        _apiClient = apiClient;
        _protector = dataProtection.CreateProtector("InstagramTokens");
        _logger = logger;
    }

    /// <summary>
    /// Get Meta App ID from settings
    /// </summary>
    public async Task<string?> GetAppIdAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == META_APP_ID);
        return setting?.Value;
    }

    /// <summary>
    /// Get Meta App Secret from settings
    /// </summary>
    public async Task<string?> GetAppSecretAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == META_APP_SECRET);
        return setting?.Value;
    }

    /// <summary>
    /// Get redirect URI from settings
    /// </summary>
    public async Task<string?> GetRedirectUriAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == META_REDIRECT_URI);
        return setting?.Value;
    }

    /// <summary>
    /// Get public asset base URL for Instagram image_url
    /// </summary>
    public async Task<string?> GetPublicAssetBaseUrlAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == PUBLIC_ASSET_BASE_URL);
        return setting?.Value;
    }

    /// <summary>
    /// Generate authorization URL for Meta OAuth
    /// </summary>
    public async Task<(string authorizeUrl, string state)?> GenerateConnectUrlAsync()
    {
        var appId = await GetAppIdAsync();
        var redirectUri = await GetRedirectUriAsync();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogWarning("Meta App ID or Redirect URI not configured");
            return null;
        }

        // Generate state for CSRF protection
        var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        // Store state in OAuthState table (reuse existing table)
        var oauthState = new OAuthState
        {
            Id = Guid.NewGuid(),
            State = state,
            CodeVerifier = "meta", // Not used for Meta OAuth, just a placeholder
            Provider = "Instagram",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };

        _db.OAuthStates.Add(oauthState);
        await _db.SaveChangesAsync();

        // Build authorization URL
        var authorizeUrl = "https://www.facebook.com/v19.0/dialog/oauth" +
            $"?client_id={Uri.EscapeDataString(appId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(DEFAULT_SCOPES)}";

        return (authorizeUrl, state);
    }

    /// <summary>
    /// Validate and consume OAuth state
    /// </summary>
    public async Task<OAuthState?> ValidateAndConsumeStateAsync(string state)
    {
        var oauthState = await _db.OAuthStates
            .FirstOrDefaultAsync(s => s.State == state && s.Provider == "Instagram");

        if (oauthState == null)
        {
            return null;
        }

        if (oauthState.ExpiresAtUtc < DateTime.UtcNow)
        {
            _db.OAuthStates.Remove(oauthState);
            await _db.SaveChangesAsync();
            return null;
        }

        // Don't remove yet - we need it for the complete step
        return oauthState;
    }

    /// <summary>
    /// Exchange code and get pages with Instagram accounts
    /// </summary>
    public async Task<InstagramExchangeResponse?> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string state,
        CancellationToken ct = default)
    {
        var appId = await GetAppIdAsync();
        var appSecret = await GetAppSecretAsync();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
        {
            _logger.LogError("Meta App ID or Secret not configured");
            return null;
        }

        // Exchange code for short-lived token
        var tokenResult = await _apiClient.ExchangeCodeForTokenAsync(code, redirectUri, appId, appSecret, ct);

        if (tokenResult.Error != null || string.IsNullOrEmpty(tokenResult.AccessToken))
        {
            var errorMsg = tokenResult.Error?.Message ?? "Unknown error during token exchange";
            _logger.LogError("Failed to exchange code: {Error}", errorMsg);
            throw new InvalidOperationException($"Facebook OAuth failed: {errorMsg}");
        }

        // Get long-lived token
        var longLivedResult = await _apiClient.GetLongLivedTokenAsync(tokenResult.AccessToken, appId, appSecret, ct);
        var accessToken = longLivedResult.AccessToken ?? tokenResult.AccessToken;
        var expiresIn = longLivedResult.ExpiresIn ?? tokenResult.ExpiresIn ?? 5184000; // ~60 days default

        // Get user info
        var me = await _apiClient.GetMeAsync(accessToken, ct);
        if (me.Error != null || string.IsNullOrEmpty(me.Id))
        {
            _logger.LogError("Failed to get user: {Error}", me.Error?.Message);
            return null;
        }

        // Get pages
        var accounts = await _apiClient.GetAccountsAsync(accessToken, ct);
        if (accounts.Error != null || accounts.Data == null)
        {
            _logger.LogError("Failed to get accounts: {Error}", accounts.Error?.Message);
            return null;
        }

        // For each page, check if it has an Instagram business account
        var pages = new List<InstagramPageInfo>();
        foreach (var page in accounts.Data)
        {
            if (string.IsNullOrEmpty(page.Id) || string.IsNullOrEmpty(page.AccessToken))
                continue;

            var igInfo = await _apiClient.GetPageInstagramAccountAsync(page.Id, page.AccessToken, ct);
            
            pages.Add(new InstagramPageInfo(
                PageId: page.Id,
                PageName: page.Name ?? "Unknown",
                PageAccessToken: page.AccessToken,
                IgUserId: igInfo.InstagramBusinessAccount?.Id,
                IgUsername: igInfo.InstagramBusinessAccount?.Username,
                HasInstagram: igInfo.InstagramBusinessAccount != null
            ));
        }

        // Store pages temporarily in cache (using OAuthState's CodeVerifier as JSON store)
        var oauthState = await _db.OAuthStates.FirstOrDefaultAsync(s => s.State == state && s.Provider == "Instagram", ct);
        if (oauthState != null)
        {
            // Store page data as JSON in CodeVerifier (hacky but works for temp storage)
            var pageData = new
            {
                FacebookUserId = me.Id,
                AccessToken = accessToken,
                ExpiresIn = expiresIn,
                Pages = pages
            };
            oauthState.CodeVerifier = System.Text.Json.JsonSerializer.Serialize(pageData);
            await _db.SaveChangesAsync(ct);
        }

        return new InstagramExchangeResponse(me.Id, pages);
    }

    /// <summary>
    /// Complete the connection by selecting a page
    /// </summary>
    public async Task<InstagramConnection?> CompleteConnectionAsync(
        string name,
        string pageId,
        string state,
        CancellationToken ct = default)
    {
        // Get stored page data
        var oauthState = await _db.OAuthStates
            .FirstOrDefaultAsync(s => s.State == state && s.Provider == "Instagram", ct);

        if (oauthState == null)
        {
            _logger.LogError("OAuth state not found");
            return null;
        }

        try
        {
            // Parse stored data
            var pageData = System.Text.Json.JsonSerializer.Deserialize<ExchangePageData>(oauthState.CodeVerifier);
            if (pageData == null)
            {
                _logger.LogError("Failed to parse stored page data");
                return null;
            }

            // Find selected page
            var selectedPage = pageData.Pages?.FirstOrDefault(p => p.PageId == pageId);
            if (selectedPage == null)
            {
                _logger.LogError("Selected page not found: {PageId}", pageId);
                return null;
            }

            if (!selectedPage.HasInstagram || string.IsNullOrEmpty(selectedPage.IgUserId))
            {
                _logger.LogError("Page {PageId} is not connected to a Professional Instagram account", pageId);
                return null;
            }

            // Check if connection already exists
            var existing = await _db.InstagramConnections
                .FirstOrDefaultAsync(c => c.IgUserId == selectedPage.IgUserId, ct);

            if (existing != null)
            {
                // Update existing connection
                existing.Name = name;
                existing.PageAccessTokenEncrypted = _protector.Protect(selectedPage.PageAccessToken);
                existing.TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(pageData.ExpiresIn);
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.IsActive = true;

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Updated Instagram connection for @{Username}", selectedPage.IgUsername);

                // Clean up OAuth state
                _db.OAuthStates.Remove(oauthState);
                await _db.SaveChangesAsync(ct);

                return existing;
            }

            // Check if this is the first connection
            var isFirst = !await _db.InstagramConnections.AnyAsync(ct);

            // Create new connection
            var connection = new InstagramConnection
            {
                Id = Guid.NewGuid(),
                Name = name,
                FacebookUserId = pageData.FacebookUserId,
                PageId = selectedPage.PageId,
                PageName = selectedPage.PageName,
                IgUserId = selectedPage.IgUserId,
                IgUsername = selectedPage.IgUsername,
                ScopesCsv = DEFAULT_SCOPES,
                PageAccessTokenEncrypted = _protector.Protect(selectedPage.PageAccessToken),
                TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(pageData.ExpiresIn),
                IsDefaultPublisher = isFirst, // First connection is default
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.InstagramConnections.Add(connection);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Created Instagram connection for @{Username} (default: {IsDefault})", 
                selectedPage.IgUsername, isFirst);

            // Clean up OAuth state
            _db.OAuthStates.Remove(oauthState);
            await _db.SaveChangesAsync(ct);

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Instagram connection");
            return null;
        }
    }

    /// <summary>
    /// Get all connections
    /// </summary>
    public async Task<List<InstagramConnection>> GetConnectionsAsync(CancellationToken ct = default)
    {
        return await _db.InstagramConnections
            .OrderByDescending(c => c.IsDefaultPublisher)
            .ThenByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Get the default publisher connection with decrypted token
    /// </summary>
    public async Task<(InstagramConnection connection, string pageAccessToken)?> GetDefaultPublisherAsync(CancellationToken ct = default)
    {
        var connection = await _db.InstagramConnections
            .Where(c => c.IsDefaultPublisher && c.IsActive)
            .FirstOrDefaultAsync(ct);

        if (connection == null)
        {
            // Fallback to any active connection
            connection = await _db.InstagramConnections
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        if (connection == null)
        {
            return null;
        }

        try
        {
            var token = _protector.Unprotect(connection.PageAccessTokenEncrypted);
            return (connection, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Instagram token for connection {Id}", connection.Id);
            return null;
        }
    }

    /// <summary>
    /// Set a connection as default
    /// </summary>
    public async Task<bool> SetDefaultAsync(Guid connectionId, CancellationToken ct = default)
    {
        var connection = await _db.InstagramConnections.FindAsync([connectionId], ct);
        if (connection == null)
        {
            return false;
        }

        // Clear all defaults
        await _db.InstagramConnections
            .Where(c => c.IsDefaultPublisher)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefaultPublisher, false), ct);

        // Set this one
        connection.IsDefaultPublisher = true;
        await _db.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Disconnect (deactivate) a connection
    /// </summary>
    public async Task<bool> DisconnectAsync(Guid connectionId, CancellationToken ct = default)
    {
        var connection = await _db.InstagramConnections.FindAsync([connectionId], ct);
        if (connection == null)
        {
            return false;
        }

        connection.IsActive = false;
        connection.IsDefaultPublisher = false;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Manually add an Instagram connection with a token from Developer Console
    /// </summary>
    public async Task<InstagramConnection?> AddManualConnectionAsync(
        string name,
        string igUserId,
        string? igUsername,
        string accessToken,
        int expiresInDays = 60,
        CancellationToken ct = default)
    {
        try
        {
            // Check if connection already exists
            var existing = await _db.InstagramConnections
                .FirstOrDefaultAsync(c => c.IgUserId == igUserId, ct);

            if (existing != null)
            {
                // Update existing connection
                existing.Name = name;
                existing.IgUsername = igUsername;
                existing.PageAccessTokenEncrypted = _protector.Protect(accessToken);
                existing.TokenExpiresAtUtc = DateTime.UtcNow.AddDays(expiresInDays);
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.IsActive = true;

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Updated manual Instagram connection for {IgUserId}", igUserId);
                return existing;
            }

            // Check if this is the first connection
            var isFirst = !await _db.InstagramConnections.AnyAsync(ct);

            // Create new connection
            var connection = new InstagramConnection
            {
                Id = Guid.NewGuid(),
                Name = name,
                FacebookUserId = "manual", // No Facebook user for manual connections
                PageId = "manual", // No page for manual connections
                PageName = "Manual Connection",
                IgUserId = igUserId,
                IgUsername = igUsername,
                ScopesCsv = "instagram_basic,instagram_content_publish",
                PageAccessTokenEncrypted = _protector.Protect(accessToken),
                TokenExpiresAtUtc = DateTime.UtcNow.AddDays(expiresInDays),
                IsDefaultPublisher = isFirst,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.InstagramConnections.Add(connection);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Created manual Instagram connection for {IgUserId} (@{Username})", 
                igUserId, igUsername);

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add manual Instagram connection");
            return null;
        }
    }

    // Helper class for deserializing stored page data
    private class ExchangePageData
    {
        public string FacebookUserId { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public long ExpiresIn { get; set; }
        public List<InstagramPageInfo>? Pages { get; set; }
    }
}

