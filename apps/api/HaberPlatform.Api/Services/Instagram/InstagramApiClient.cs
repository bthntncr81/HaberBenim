using System.Text.Json;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.Instagram;

/// <summary>
/// Client for Meta Graph API - Instagram publishing
/// </summary>
public class InstagramApiClient
{
    private readonly HttpClient _httpClient;
    private readonly MetaGraphOptions _options;
    private readonly ILogger<InstagramApiClient> _logger;

    public InstagramApiClient(
        HttpClient httpClient,
        IOptions<MetaGraphOptions> options,
        ILogger<InstagramApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Exchange OAuth code for user access token
    /// </summary>
    public async Task<MetaTokenResponse> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri,
        string appId,
        string appSecret,
        CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(appId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&code={Uri.EscapeDataString(code)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("Token exchange response: {Status} - {Content}", 
                response.StatusCode, content.Length > 500 ? content[..500] : content);

            // Check if response is HTML (error page)
            if (content.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Facebook returned HTML instead of JSON. Status: {Status}", response.StatusCode);
                return new MetaTokenResponse 
                { 
                    Error = new MetaErrorResponse 
                    { 
                        Message = $"Facebook returned an error page (HTTP {(int)response.StatusCode}). This usually means the App ID, App Secret, or Redirect URI is invalid. Please check your Facebook App configuration.",
                        Code = (int)response.StatusCode
                    } 
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                // Try to parse error from JSON
                try
                {
                    var errorResult = JsonSerializer.Deserialize<MetaTokenResponse>(content);
                    if (errorResult?.Error != null)
                        return errorResult;
                }
                catch { }

                return new MetaTokenResponse 
                { 
                    Error = new MetaErrorResponse 
                    { 
                        Message = $"Facebook API error: HTTP {(int)response.StatusCode} - {content}",
                        Code = (int)response.StatusCode
                    } 
                };
            }

            var result = JsonSerializer.Deserialize<MetaTokenResponse>(content);
            return result ?? new MetaTokenResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Facebook response as JSON");
            return new MetaTokenResponse { Error = new MetaErrorResponse { Message = "Invalid response from Facebook. Please check your App configuration." } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange code for token");
            return new MetaTokenResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Exchange short-lived token for long-lived token (~60 days)
    /// </summary>
    public async Task<MetaTokenResponse> GetLongLivedTokenAsync(
        string shortLivedToken,
        string appId,
        string appSecret,
        CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/oauth/access_token" +
            $"?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(appId)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(shortLivedToken)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            var result = JsonSerializer.Deserialize<MetaTokenResponse>(content);
            return result ?? new MetaTokenResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get long-lived token");
            return new MetaTokenResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Get current user info
    /// </summary>
    public async Task<MetaMeResponse> GetMeAsync(string accessToken, CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/me?access_token={Uri.EscapeDataString(accessToken)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            var result = JsonSerializer.Deserialize<MetaMeResponse>(content);
            return result ?? new MetaMeResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get /me");
            return new MetaMeResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Get pages the user has access to
    /// </summary>
    public async Task<MetaAccountsResponse> GetAccountsAsync(string accessToken, CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/me/accounts" +
            $"?fields=id,name,access_token,category" +
            $"&access_token={Uri.EscapeDataString(accessToken)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("Get accounts response: {Content}", 
                content.Length > 1000 ? content[..1000] : content);

            var result = JsonSerializer.Deserialize<MetaAccountsResponse>(content);
            return result ?? new MetaAccountsResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get accounts");
            return new MetaAccountsResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Get Instagram business account connected to a page
    /// </summary>
    public async Task<MetaPageInstagramResponse> GetPageInstagramAccountAsync(
        string pageId,
        string pageAccessToken,
        CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/{pageId}" +
            $"?fields=instagram_business_account{{id,username}}" +
            $"&access_token={Uri.EscapeDataString(pageAccessToken)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("Get page Instagram account response: {Content}", content);

            var result = JsonSerializer.Deserialize<MetaPageInstagramResponse>(content);
            return result ?? new MetaPageInstagramResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get page Instagram account for {PageId}", pageId);
            return new MetaPageInstagramResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Create an Instagram media container (step 1 of publishing)
    /// </summary>
    public async Task<MetaMediaContainerResponse> CreateMediaContainerAsync(
        string igUserId,
        string imageUrl,
        string caption,
        string pageAccessToken,
        CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/{igUserId}/media";

        var parameters = new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["caption"] = caption,
            ["access_token"] = pageAccessToken
        };

        try
        {
            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("Create media container response: {Status} - {Content}",
                response.StatusCode, responseContent);

            if ((int)response.StatusCode == 429)
            {
                return new MetaMediaContainerResponse
                {
                    Error = new MetaErrorResponse
                    {
                        Message = "Rate limit exceeded",
                        Code = 4
                    }
                };
            }

            var result = JsonSerializer.Deserialize<MetaMediaContainerResponse>(responseContent);
            return result ?? new MetaMediaContainerResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create media container");
            return new MetaMediaContainerResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Publish a media container (step 2 of publishing)
    /// </summary>
    public async Task<MetaMediaPublishResponse> PublishMediaAsync(
        string igUserId,
        string creationId,
        string pageAccessToken,
        CancellationToken ct = default)
    {
        var url = $"{_options.ApiBaseUrl}/{igUserId}/media_publish";

        var parameters = new Dictionary<string, string>
        {
            ["creation_id"] = creationId,
            ["access_token"] = pageAccessToken
        };

        try
        {
            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("Publish media response: {Status} - {Content}",
                response.StatusCode, responseContent);

            if ((int)response.StatusCode == 429)
            {
                return new MetaMediaPublishResponse
                {
                    Error = new MetaErrorResponse
                    {
                        Message = "Rate limit exceeded",
                        Code = 4
                    }
                };
            }

            var result = JsonSerializer.Deserialize<MetaMediaPublishResponse>(responseContent);
            return result ?? new MetaMediaPublishResponse { Error = new MetaErrorResponse { Message = "Empty response" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish media");
            return new MetaMediaPublishResponse { Error = new MetaErrorResponse { Message = ex.Message } };
        }
    }

    /// <summary>
    /// Full publish flow: create container then publish
    /// </summary>
    public async Task<InstagramPublishResult> PublishImageAsync(
        string igUserId,
        string imageUrl,
        string caption,
        string pageAccessToken,
        CancellationToken ct = default)
    {
        // Step 1: Create media container
        var containerResult = await CreateMediaContainerAsync(igUserId, imageUrl, caption, pageAccessToken, ct);

        if (containerResult.Error != null)
        {
            var isRateLimit = containerResult.Error.Code == 4 || 
                              containerResult.Error.Code == 32 ||
                              containerResult.Error.Message?.Contains("rate", StringComparison.OrdinalIgnoreCase) == true;

            var isAuth = containerResult.Error.Code == 190 || 
                         containerResult.Error.Code == 102 ||
                         containerResult.Error.Type?.Contains("OAuthException", StringComparison.OrdinalIgnoreCase) == true;

            return new InstagramPublishResult
            {
                Success = false,
                Error = containerResult.Error.Message,
                IsRateLimited = isRateLimit,
                IsAuthError = isAuth
            };
        }

        if (string.IsNullOrEmpty(containerResult.Id))
        {
            return new InstagramPublishResult
            {
                Success = false,
                Error = "No container ID returned"
            };
        }

        _logger.LogInformation("Created Instagram media container: {ContainerId}", containerResult.Id);

        // Step 2: Wait for container to be ready and publish
        // Instagram needs time to process the media before publishing
        const int maxRetries = 5;
        const int retryDelayMs = 5000; // 5 seconds between retries
        
        MetaMediaPublishResponse? publishResult = null;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            // Check container status first
            var statusUrl = $"{_options.ApiBaseUrl}/{containerResult.Id}?fields=status,status_code&access_token={pageAccessToken}";
            var statusResponse = await _httpClient.GetAsync(statusUrl, ct);
            var statusContent = await statusResponse.Content.ReadAsStringAsync(ct);
            
            _logger.LogDebug("Container status check {Attempt}/{Max}: {Content}", attempt, maxRetries, statusContent);
            
            // Parse status
            var statusCheck = JsonSerializer.Deserialize<JsonElement>(statusContent);
            var statusCode = statusCheck.TryGetProperty("status_code", out var sc) ? sc.GetString() : null;
            
            if (statusCode == "FINISHED")
            {
                _logger.LogInformation("Container {ContainerId} is ready, publishing...", containerResult.Id);
            }
            else if (statusCode == "ERROR" || statusCode == "EXPIRED")
            {
                return new InstagramPublishResult
                {
                    Success = false,
                    ContainerId = containerResult.Id,
                    Error = $"Container processing failed: {statusCode}"
                };
            }
            else if (attempt < maxRetries)
            {
                _logger.LogInformation("Container not ready (status: {Status}), waiting {Delay}ms before retry {Attempt}/{Max}",
                    statusCode, retryDelayMs, attempt + 1, maxRetries);
                await Task.Delay(retryDelayMs, ct);
                continue;
            }
            
            // Try to publish
            publishResult = await PublishMediaAsync(igUserId, containerResult.Id, pageAccessToken, ct);
            
            // Check for "media not ready" error (code 9007)
            if (publishResult.Error != null && publishResult.Error.Code == 9007)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Media not ready (error 9007), waiting {Delay}ms before retry {Attempt}/{Max}",
                        retryDelayMs, attempt + 1, maxRetries);
                    await Task.Delay(retryDelayMs, ct);
                    continue;
                }
            }
            
            // If we got a result (success or other error), break
            break;
        }

        if (publishResult == null)
        {
            return new InstagramPublishResult
            {
                Success = false,
                ContainerId = containerResult.Id,
                Error = "Failed to publish after retries"
            };
        }

        if (publishResult.Error != null)
        {
            var isRateLimit = publishResult.Error.Code == 4 || 
                              publishResult.Error.Code == 32 ||
                              publishResult.Error.Message?.Contains("rate", StringComparison.OrdinalIgnoreCase) == true;

            var isAuth = publishResult.Error.Code == 190 || 
                         publishResult.Error.Code == 102 ||
                         publishResult.Error.Type?.Contains("OAuthException", StringComparison.OrdinalIgnoreCase) == true;

            return new InstagramPublishResult
            {
                Success = false,
                ContainerId = containerResult.Id,
                Error = publishResult.Error.Message,
                IsRateLimited = isRateLimit,
                IsAuthError = isAuth
            };
        }

        if (string.IsNullOrEmpty(publishResult.Id))
        {
            return new InstagramPublishResult
            {
                Success = false,
                ContainerId = containerResult.Id,
                Error = "No media ID returned from publish"
            };
        }

        _logger.LogInformation("Published Instagram media: {MediaId}", publishResult.Id);

        return new InstagramPublishResult
        {
            Success = true,
            ContainerId = containerResult.Id,
            MediaId = publishResult.Id
        };
    }
}

