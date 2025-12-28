using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.XIntegration;

/// <summary>
/// Handles OAuth2 PKCE flow for X (Twitter) API integration
/// </summary>
public class XOAuthService
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<XOAuthService> _logger;
    private readonly IConfiguration _configuration;

    private const string X_AUTH_URL = "https://twitter.com/i/oauth2/authorize";
    private const string X_TOKEN_URL = "https://api.x.com/2/oauth2/token";
    private const string X_USERS_ME_URL = "https://api.x.com/2/users/me";

    public XOAuthService(
        AppDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<XOAuthService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("XTokens");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    #region PKCE Helpers

    /// <summary>
    /// Generate PKCE code_verifier and code_challenge
    /// </summary>
    public (string CodeVerifier, string CodeChallenge) GeneratePkce()
    {
        // Generate 32 random bytes for code_verifier
        var bytes = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = Base64UrlEncode(bytes);

        // Generate code_challenge using SHA256
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);

        return (codeVerifier, codeChallenge);
    }

    /// <summary>
    /// Generate random state for CSRF protection
    /// </summary>
    public string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    #endregion

    #region Configuration

    public string GetClientId()
    {
        return GetSystemSetting("X_CLIENT_ID") 
            ?? _configuration["X:ClientId"] 
            ?? throw new InvalidOperationException("X_CLIENT_ID not configured");
    }

    public string? GetClientSecret()
    {
        return GetSystemSetting("X_CLIENT_SECRET") ?? _configuration["X:ClientSecret"];
    }

    public string GetRedirectUri()
    {
        return GetSystemSetting("X_REDIRECT_URI") 
            ?? _configuration["X:RedirectUri"]
            ?? throw new InvalidOperationException("X_REDIRECT_URI not configured");
    }

    public string? GetAppBearerToken()
    {
        // Backward/forward compatibility:
        // - canonical key: X_APP_BEARER_TOKEN (seeded)
        // - legacy key (used by older admin UI): X_BEARER_TOKEN
        var raw =
            GetSystemSetting("X_APP_BEARER_TOKEN")
            ?? GetSystemSetting("X_BEARER_TOKEN")
            ?? _configuration["X:AppBearerToken"];

        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();

        // Some users paste URL-encoded bearer tokens (e.g. %3D instead of '=')
        // Decode safely if it looks encoded.
        if (trimmed.Contains('%'))
        {
            try
            {
                trimmed = Uri.UnescapeDataString(trimmed);
            }
            catch
            {
                // ignore decode errors and keep original
            }
        }

        return trimmed;
    }

    public string GetApiBaseUrl()
    {
        return GetSystemSetting("X_API_BASE_URL") 
            ?? _configuration["X:ApiBaseUrl"]
            ?? "https://api.x.com";
    }

    // ===== OAuth 1.0a Credentials =====

    public string? GetApiKey()
    {
        return GetSystemSetting("X_API_KEY") ?? _configuration["X:ApiKey"];
    }

    public string? GetApiSecretKey()
    {
        return GetSystemSetting("X_API_SECRET_KEY") ?? _configuration["X:ApiSecretKey"];
    }

    public string? GetAccessToken()
    {
        return GetSystemSetting("X_ACCESS_TOKEN") ?? _configuration["X:AccessToken"];
    }

    public string? GetAccessTokenSecret()
    {
        return GetSystemSetting("X_ACCESS_TOKEN_SECRET") ?? _configuration["X:AccessTokenSecret"];
    }

    /// <summary>
    /// Check if OAuth 1.0a credentials are fully configured
    /// </summary>
    public bool HasOAuth1Credentials()
    {
        var apiKey = GetApiKey();
        var apiSecret = GetApiSecretKey();
        var accessToken = GetAccessToken();
        var accessTokenSecret = GetAccessTokenSecret();

        return !string.IsNullOrWhiteSpace(apiKey) 
            && !string.IsNullOrWhiteSpace(apiSecret)
            && !string.IsNullOrWhiteSpace(accessToken)
            && !string.IsNullOrWhiteSpace(accessTokenSecret);
    }

    /// <summary>
    /// Get OAuth 1.0a credentials if available
    /// </summary>
    public OAuth1Credentials? GetOAuth1Credentials()
    {
        var apiKey = GetApiKey();
        var apiSecret = GetApiSecretKey();
        var accessToken = GetAccessToken();
        var accessTokenSecret = GetAccessTokenSecret();

        if (string.IsNullOrWhiteSpace(apiKey) 
            || string.IsNullOrWhiteSpace(apiSecret)
            || string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(accessTokenSecret))
        {
            return null;
        }

        return new OAuth1Credentials
        {
            ApiKey = apiKey,
            ApiSecretKey = apiSecret,
            AccessToken = accessToken,
            AccessTokenSecret = accessTokenSecret
        };
    }

    private string? GetSystemSetting(string key)
    {
        return _db.SystemSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefault();
    }

    #endregion

    #region OAuth Flow

    /// <summary>
    /// Build the authorization URL for X OAuth2
    /// </summary>
    public string BuildAuthorizeUrl(string codeChallenge, string state, string scopes = "tweet.read tweet.write users.read offline.access")
    {
        var clientId = GetClientId();
        var redirectUri = GetRedirectUri();

        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scopes,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", queryParams.Select(kv => 
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{X_AUTH_URL}?{queryString}";
    }

    /// <summary>
    /// Store OAuth state for callback validation
    /// </summary>
    public async Task StoreOAuthStateAsync(string state, string codeVerifier)
    {
        var oauthState = new OAuthState
        {
            State = state,
            CodeVerifier = codeVerifier,
            Provider = "X",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };

        _db.OAuthStates.Add(oauthState);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Validate and consume OAuth state
    /// </summary>
    public async Task<OAuthState?> ValidateAndConsumeStateAsync(string state)
    {
        var oauthState = await _db.OAuthStates
            .FirstOrDefaultAsync(s => s.State == state && s.ExpiresAtUtc > DateTime.UtcNow);

        if (oauthState != null)
        {
            _db.OAuthStates.Remove(oauthState);
            await _db.SaveChangesAsync();
        }

        return oauthState;
    }

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    public async Task<XTokenResponse> ExchangeCodeForTokenAsync(string code, string codeVerifier)
    {
        var clientId = GetClientId();
        var clientSecret = GetClientSecret();
        var redirectUri = GetRedirectUri();

        var client = _httpClientFactory.CreateClient("XApi");

        var formData = new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        };

        var request = new HttpRequestMessage(HttpMethod.Post, X_TOKEN_URL)
        {
            Content = new FormUrlEncodedContent(formData)
        };

        // Add Basic auth if client_secret is configured (confidential client)
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        _logger.LogDebug("Token exchange response: {StatusCode} {Body}", response.StatusCode, json);

        var tokenResponse = JsonSerializer.Deserialize<XTokenResponse>(json) 
            ?? throw new InvalidOperationException("Failed to parse token response");

        if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(tokenResponse.Error))
        {
            throw new InvalidOperationException($"Token exchange failed: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
        }

        return tokenResponse;
    }

    /// <summary>
    /// Refresh an expired access token
    /// </summary>
    public async Task<XTokenResponse> RefreshAccessTokenAsync(string refreshToken)
    {
        var clientId = GetClientId();
        var clientSecret = GetClientSecret();

        var client = _httpClientFactory.CreateClient("XApi");

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, X_TOKEN_URL)
        {
            Content = new FormUrlEncodedContent(formData)
        };

        // Add Basic auth if client_secret is configured
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        _logger.LogDebug("Token refresh response: {StatusCode}", response.StatusCode);

        var tokenResponse = JsonSerializer.Deserialize<XTokenResponse>(json)
            ?? throw new InvalidOperationException("Failed to parse refresh token response");

        if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(tokenResponse.Error))
        {
            throw new InvalidOperationException($"Token refresh failed: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
        }

        return tokenResponse;
    }

    /// <summary>
    /// Get current user info using access token
    /// </summary>
    public async Task<XUserData> GetCurrentUserAsync(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("XApi");
        
        var request = new HttpRequestMessage(HttpMethod.Get, X_USERS_ME_URL);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get current user: {StatusCode} {Body}", response.StatusCode, json);
            throw new InvalidOperationException($"Failed to get current user: {response.StatusCode}");
        }

        var userResponse = JsonSerializer.Deserialize<XUserResponse>(json);
        return userResponse?.Data ?? throw new InvalidOperationException("Failed to parse user response");
    }

    #endregion

    #region Token Encryption

    public string EncryptToken(string token)
    {
        return _protector.Protect(token);
    }

    public string DecryptToken(string encryptedToken)
    {
        return _protector.Unprotect(encryptedToken);
    }

    #endregion

    #region Connection Management

    /// <summary>
    /// Create or update X connection after successful OAuth
    /// </summary>
    public async Task<XIntegrationConnection> CreateOrUpdateConnectionAsync(
        XTokenResponse tokens,
        XUserData user,
        string name = "Default Connection")
    {
        // Check for existing connection with same XUserId
        var existing = await _db.XIntegrationConnections
            .FirstOrDefaultAsync(c => c.XUserId == user.Id);

        var isFirst = !await _db.XIntegrationConnections.AnyAsync();

        if (existing != null)
        {
            // Update existing connection
            existing.AccessTokenEncrypted = EncryptToken(tokens.AccessToken!);
            existing.RefreshTokenEncrypted = EncryptToken(tokens.RefreshToken!);
            existing.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            existing.ScopesCsv = tokens.Scope ?? "";
            existing.XUsername = user.Username ?? "";
            existing.IsActive = true;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            // Create new connection
            existing = new XIntegrationConnection
            {
                Name = name,
                XUserId = user.Id!,
                XUsername = user.Username ?? "",
                ScopesCsv = tokens.Scope ?? "",
                AccessTokenEncrypted = EncryptToken(tokens.AccessToken!),
                RefreshTokenEncrypted = EncryptToken(tokens.RefreshToken!),
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn),
                IsDefaultPublisher = isFirst, // First connection becomes default
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.XIntegrationConnections.Add(existing);
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Get default publisher connection with valid token
    /// </summary>
    public async Task<(XIntegrationConnection Connection, string AccessToken)?> GetDefaultPublisherAsync()
    {
        var connection = await _db.XIntegrationConnections
            .Where(c => c.IsDefaultPublisher && c.IsActive)
            .FirstOrDefaultAsync();

        if (connection == null) return null;

        // Check if token needs refresh (expires in less than 2 minutes)
        if (connection.AccessTokenExpiresAtUtc < DateTime.UtcNow.AddMinutes(2))
        {
            try
            {
                var refreshToken = DecryptToken(connection.RefreshTokenEncrypted);
                var newTokens = await RefreshAccessTokenAsync(refreshToken);

                connection.AccessTokenEncrypted = EncryptToken(newTokens.AccessToken!);
                connection.RefreshTokenEncrypted = EncryptToken(newTokens.RefreshToken!);
                connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(newTokens.ExpiresIn);
                connection.UpdatedAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return (connection, newTokens.AccessToken!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh token for connection {ConnectionId}", connection.Id);
                connection.IsActive = false;
                await _db.SaveChangesAsync();
                return null;
            }
        }

        return (connection, DecryptToken(connection.AccessTokenEncrypted));
    }

    /// <summary>
    /// Get all connections
    /// </summary>
    public async Task<List<XIntegrationConnection>> GetConnectionsAsync()
    {
        return await _db.XIntegrationConnections
            .OrderByDescending(c => c.IsDefaultPublisher)
            .ThenByDescending(c => c.CreatedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Set a connection as the default publisher
    /// </summary>
    public async Task<bool> SetDefaultPublisherAsync(Guid connectionId)
    {
        var connection = await _db.XIntegrationConnections.FindAsync(connectionId);
        if (connection == null) return false;

        // Clear other defaults
        await _db.XIntegrationConnections
            .Where(c => c.IsDefaultPublisher)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefaultPublisher, false));

        connection.IsDefaultPublisher = true;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Disconnect (deactivate) a connection
    /// </summary>
    public async Task<bool> DisconnectAsync(Guid connectionId)
    {
        var connection = await _db.XIntegrationConnections.FindAsync(connectionId);
        if (connection == null) return false;

        connection.IsActive = false;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return true;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Clean up expired OAuth states
    /// </summary>
    public async Task CleanupExpiredStatesAsync()
    {
        await _db.OAuthStates
            .Where(s => s.ExpiresAtUtc < DateTime.UtcNow)
            .ExecuteDeleteAsync();
    }

    #endregion
}

