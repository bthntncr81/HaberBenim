using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.XIntegration;

/// <summary>
/// OAuth 1.0a credentials for X API
/// </summary>
public class OAuth1Credentials
{
    public required string ApiKey { get; set; }
    public required string ApiSecretKey { get; set; }
    public required string AccessToken { get; set; }
    public required string AccessTokenSecret { get; set; }
}

/// <summary>
/// Typed HTTP client for X (Twitter) API v2
/// Supports both OAuth 2.0 Bearer tokens and OAuth 1.0a user context
/// </summary>
public class XApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XApiClient> _logger;

    public XApiClient(HttpClient httpClient, ILogger<XApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    #region App-Only Requests (Bearer Token)

    /// <summary>
    /// Make an authenticated GET request using app-only bearer token
    /// </summary>
    public async Task<XApiResult<T>> AppOnlyGetAsync<T>(string path, string appBearerToken, Dictionary<string, string>? queryParams = null)
    {
        var url = BuildUrl(path, queryParams);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appBearerToken);

        return await SendRequestAsync<T>(request);
    }

    #endregion

    #region User Context Requests (OAuth2)

    /// <summary>
    /// Make an authenticated GET request using user access token (OAuth 2.0)
    /// </summary>
    public async Task<XApiResult<T>> UserGetAsync<T>(string path, string accessToken, Dictionary<string, string>? queryParams = null)
    {
        var url = BuildUrl(path, queryParams);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await SendRequestAsync<T>(request);
    }

    /// <summary>
    /// Make an authenticated POST request using user access token (OAuth 2.0)
    /// </summary>
    public async Task<XApiResult<T>> UserPostAsync<T>(string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await SendRequestAsync<T>(request);
    }

    #endregion

    #region OAuth 1.0a Requests

    /// <summary>
    /// Make an authenticated GET request using OAuth 1.0a credentials
    /// </summary>
    public async Task<XApiResult<T>> OAuth1GetAsync<T>(string path, OAuth1Credentials credentials, Dictionary<string, string>? queryParams = null)
    {
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "https://api.x.com";
        var fullUrl = BuildUrl($"{baseUrl}{path}", queryParams);

        var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
        
        var authHeader = OAuth1Helper.GenerateAuthHeader(
            "GET",
            fullUrl,
            credentials.ApiKey,
            credentials.ApiSecretKey,
            credentials.AccessToken,
            credentials.AccessTokenSecret
        );
        
        request.Headers.Add("Authorization", authHeader);

        return await SendRequestAsync<T>(request);
    }

    /// <summary>
    /// Make an authenticated POST request using OAuth 1.0a credentials
    /// </summary>
    public async Task<XApiResult<T>> OAuth1PostAsync<T>(string path, OAuth1Credentials credentials, object? body = null)
    {
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "https://api.x.com";
        var fullUrl = $"{baseUrl}{path}";

        var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var authHeader = OAuth1Helper.GenerateAuthHeader(
            "POST",
            fullUrl,
            credentials.ApiKey,
            credentials.ApiSecretKey,
            credentials.AccessToken,
            credentials.AccessTokenSecret
        );
        
        request.Headers.Add("Authorization", authHeader);

        return await SendRequestAsync<T>(request);
    }

    /// <summary>
    /// Make an authenticated DELETE request using OAuth 1.0a credentials
    /// </summary>
    public async Task<XApiResult<T>> OAuth1DeleteAsync<T>(string path, OAuth1Credentials credentials)
    {
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "https://api.x.com";
        var fullUrl = $"{baseUrl}{path}";

        var request = new HttpRequestMessage(HttpMethod.Delete, fullUrl);

        var authHeader = OAuth1Helper.GenerateAuthHeader(
            "DELETE",
            fullUrl,
            credentials.ApiKey,
            credentials.ApiSecretKey,
            credentials.AccessToken,
            credentials.AccessTokenSecret
        );
        
        request.Headers.Add("Authorization", authHeader);

        return await SendRequestAsync<T>(request);
    }

    #endregion

    #region Specific API Methods

    /// <summary>
    /// Get user by username (using Bearer token)
    /// </summary>
    public async Task<XApiResult<XUserByUsernameResponse>> GetUserByUsernameAsync(string username, string bearerToken, bool isAppToken = true)
    {
        var path = $"/2/users/by/username/{username}";
        
        if (isAppToken)
            return await AppOnlyGetAsync<XUserByUsernameResponse>(path, bearerToken);
        else
            return await UserGetAsync<XUserByUsernameResponse>(path, bearerToken);
    }

    /// <summary>
    /// Get user by username using OAuth 1.0a credentials
    /// </summary>
    public async Task<XApiResult<XUserByUsernameResponse>> GetUserByUsernameOAuth1Async(string username, OAuth1Credentials credentials)
    {
        var path = $"/2/users/by/username/{username}";
        return await OAuth1GetAsync<XUserByUsernameResponse>(path, credentials);
    }

    /// <summary>
    /// Get tweets by user ID with since_id for pagination (using Bearer token)
    /// </summary>
    public async Task<XApiResult<XTweetsResponse>> GetUserTweetsAsync(
        string userId,
        string bearerToken,
        int maxResults = 10,
        string? sinceId = null,
        bool isAppToken = true)
    {
        var path = $"/2/users/{userId}/tweets";
        var queryParams = new Dictionary<string, string>
        {
            ["max_results"] = maxResults.ToString(),
            ["tweet.fields"] = "created_at,author_id,conversation_id,entities,attachments"
        };

        if (!string.IsNullOrEmpty(sinceId))
        {
            queryParams["since_id"] = sinceId;
        }

        if (isAppToken)
            return await AppOnlyGetAsync<XTweetsResponse>(path, bearerToken, queryParams);
        else
            return await UserGetAsync<XTweetsResponse>(path, bearerToken, queryParams);
    }

    /// <summary>
    /// Get tweets by user ID using OAuth 1.0a
    /// </summary>
    public async Task<XApiResult<XTweetsResponse>> GetUserTweetsOAuth1Async(
        string userId,
        OAuth1Credentials credentials,
        int maxResults = 10,
        string? sinceId = null)
    {
        var path = $"/2/users/{userId}/tweets";
        var queryParams = new Dictionary<string, string>
        {
            ["max_results"] = maxResults.ToString(),
            ["tweet.fields"] = "created_at,author_id,conversation_id,entities,attachments"
        };

        if (!string.IsNullOrEmpty(sinceId))
        {
            queryParams["since_id"] = sinceId;
        }

        return await OAuth1GetAsync<XTweetsResponse>(path, credentials, queryParams);
    }

    /// <summary>
    /// Post a new tweet using OAuth 2.0 Bearer token
    /// </summary>
    public async Task<XApiResult<XPostTweetResponse>> PostTweetAsync(string accessToken, string text)
    {
        var body = new XPostTweetRequest { Text = text };
        return await UserPostAsync<XPostTweetResponse>("/2/tweets", accessToken, body);
    }

    /// <summary>
    /// Post a new tweet using OAuth 1.0a credentials
    /// </summary>
    public async Task<XApiResult<XPostTweetResponse>> PostTweetOAuth1Async(OAuth1Credentials credentials, string text)
    {
        var body = new XPostTweetRequest { Text = text };
        return await OAuth1PostAsync<XPostTweetResponse>("/2/tweets", credentials, body);
    }

    /// <summary>
    /// Delete a tweet using OAuth 1.0a credentials
    /// </summary>
    public async Task<XApiResult<XDeleteTweetResponse>> DeleteTweetOAuth1Async(OAuth1Credentials credentials, string tweetId)
    {
        return await OAuth1DeleteAsync<XDeleteTweetResponse>($"/2/tweets/{tweetId}", credentials);
    }

    /// <summary>
    /// Get authenticated user info using OAuth 1.0a
    /// </summary>
    public async Task<XApiResult<XUserResponse>> GetMeOAuth1Async(OAuth1Credentials credentials)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["user.fields"] = "id,name,username,profile_image_url,description,created_at,public_metrics"
        };
        return await OAuth1GetAsync<XUserResponse>("/2/users/me", credentials, queryParams);
    }

    /// <summary>
    /// Search recent tweets using Bearer token
    /// </summary>
    public async Task<XApiResult<XTweetsResponse>> SearchRecentTweetsAsync(
        string query,
        string bearerToken,
        int maxResults = 10,
        string? sinceId = null)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["query"] = query,
            ["max_results"] = maxResults.ToString(),
            ["tweet.fields"] = "created_at,author_id,conversation_id,entities,attachments,public_metrics"
        };

        if (!string.IsNullOrEmpty(sinceId))
        {
            queryParams["since_id"] = sinceId;
        }

        return await AppOnlyGetAsync<XTweetsResponse>("/2/tweets/search/recent", bearerToken, queryParams);
    }

    /// <summary>
    /// Get tweet by ID using Bearer token
    /// </summary>
    public async Task<XApiResult<XSingleTweetResponse>> GetTweetAsync(string tweetId, string bearerToken)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["tweet.fields"] = "created_at,author_id,conversation_id,entities,attachments,public_metrics"
        };

        return await AppOnlyGetAsync<XSingleTweetResponse>($"/2/tweets/{tweetId}", bearerToken, queryParams);
    }

    /// <summary>
    /// Get user by ID using Bearer token
    /// </summary>
    public async Task<XApiResult<XUserResponse>> GetUserByIdAsync(string userId, string bearerToken)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["user.fields"] = "id,name,username,profile_image_url,description,created_at,public_metrics"
        };

        return await AppOnlyGetAsync<XUserResponse>($"/2/users/{userId}", bearerToken, queryParams);
    }

    #endregion

    #region Helpers

    private string BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
            return path;

        var queryString = string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{path}?{queryString}";
    }

    private async Task<XApiResult<T>> SendRequestAsync<T>(HttpRequestMessage request)
    {
        try
        {
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("X API {Method} {Path}: {StatusCode}", 
                request.Method, request.RequestUri, response.StatusCode);

            // Check for rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var resetHeader = response.Headers.TryGetValues("x-rate-limit-reset", out var resetValues)
                    ? resetValues.FirstOrDefault()
                    : null;
                var resetTime = ParseResetHeader(resetHeader);

                _logger.LogWarning("X API rate limited. Reset at: {ResetTime}", resetTime);

                return XApiResult<T>.RateLimited(resetTime);
            }

            // Check for auth errors
            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("X API auth error: {StatusCode} {Body}", response.StatusCode, json);
                return XApiResult<T>.AuthError(
                    $"Authentication failed: {response.StatusCode}",
                    (int)response.StatusCode,
                    json);
            }

            // Parse success response
            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<T>(json);
                return XApiResult<T>.Success(data!, json);
            }

            // Parse error response
            var errorResponse = JsonSerializer.Deserialize<XApiErrorResponse>(json);
            return XApiResult<T>.Failed(
                errorResponse?.Detail ?? errorResponse?.Title ?? $"API error: {response.StatusCode}",
                (int)response.StatusCode);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError("X API timeout: {RequestUri}", request.RequestUri);
            return XApiResult<T>.Failed("Request timeout", 408);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "X API request failed: {RequestUri}", request.RequestUri);
            return XApiResult<T>.Failed(ex.Message, 0);
        }
    }

    private DateTime? ParseResetHeader(string? resetHeader)
    {
        if (string.IsNullOrEmpty(resetHeader)) return null;
        
        if (long.TryParse(resetHeader, out var unixTime))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        }
        
        return null;
    }

    #endregion
}

/// <summary>
/// Result wrapper for X API calls with rate limiting support
/// </summary>
public class XApiResult<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsRateLimited { get; private set; }
    public bool IsAuthError { get; private set; }
    public T? Data { get; private set; }
    public string? RawJson { get; private set; }
    public string? Error { get; private set; }
    public int StatusCode { get; private set; }
    public DateTime? RateLimitResetAt { get; private set; }

    public static XApiResult<T> Success(T data, string rawJson) => new()
    {
        IsSuccess = true,
        Data = data,
        RawJson = rawJson,
        StatusCode = 200
    };

    public static XApiResult<T> Failed(string error, int statusCode) => new()
    {
        IsSuccess = false,
        Error = error,
        StatusCode = statusCode
    };

    public static XApiResult<T> RateLimited(DateTime? resetAt) => new()
    {
        IsSuccess = false,
        IsRateLimited = true,
        RateLimitResetAt = resetAt,
        Error = "Rate limited",
        StatusCode = 429
    };

    public static XApiResult<T> AuthError(string error, int statusCode, string? rawJson = null) => new()
    {
        IsSuccess = false,
        IsAuthError = true,
        Error = error,
        StatusCode = statusCode,
        RawJson = rawJson
    };
}
