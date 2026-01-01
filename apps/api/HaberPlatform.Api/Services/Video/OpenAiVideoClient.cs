using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using Microsoft.Extensions.Options;

namespace HaberPlatform.Api.Services.Video;

/// <summary>
/// Client for OpenAI Video API (Sora)
/// </summary>
public class OpenAiVideoClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiVideoOptions _options;
    private readonly OpenAiConfigService _configService;
    private readonly ILogger<OpenAiVideoClient> _logger;

    public OpenAiVideoClient(
        HttpClient httpClient,
        IOptions<OpenAiVideoOptions> options,
        OpenAiConfigService configService,
        ILogger<OpenAiVideoClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configService = configService;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    /// Get API key from config service (database) or fall back to options (appsettings)
    /// </summary>
    private async Task<string?> GetApiKeyAsync()
    {
        // First try database (encrypted)
        var dbKey = await _configService.GetDecryptedApiKeyAsync();
        if (!string.IsNullOrEmpty(dbKey))
        {
            return dbKey;
        }
        
        // Fall back to appsettings
        return _options.ApiKey;
    }

    /// <summary>
    /// Create a new video generation job
    /// </summary>
    public async Task<OpenAiVideoCreateResponse> CreateVideoAsync(
        string prompt,
        string model = "sora-2",
        string seconds = "8",
        string size = "1280x720",
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating OpenAI video: model={Model}, seconds={Seconds}, size={Size}", 
            model, seconds, size);

        // Validate parameters
        if (!OpenAiVideoOptions.AllowedModels.Contains(model))
        {
            throw new ArgumentException($"Invalid model: {model}. Allowed: {string.Join(", ", OpenAiVideoOptions.AllowedModels)}");
        }
        if (!OpenAiVideoOptions.AllowedSeconds.Contains(seconds))
        {
            throw new ArgumentException($"Invalid seconds: {seconds}. Allowed: {string.Join(", ", OpenAiVideoOptions.AllowedSeconds)}");
        }
        if (!OpenAiVideoOptions.AllowedSizes.Contains(size))
        {
            throw new ArgumentException($"Invalid size: {size}. Allowed: {string.Join(", ", OpenAiVideoOptions.AllowedSizes)}");
        }

        // Get API key
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            return new OpenAiVideoCreateResponse
            {
                Status = "failed",
                Error = new OpenAiVideoError { Message = "OpenAI API key not configured" }
            };
        }

        try
        {
            // Build multipart form data
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(model), "model");
            content.Add(new StringContent(prompt), "prompt");
            content.Add(new StringContent(seconds), "seconds");
            content.Add(new StringContent(size), "size");

            // Create request with auth header
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/videos");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI video creation failed: {Status} - {Body}", 
                    response.StatusCode, responseBody);
                
                return new OpenAiVideoCreateResponse
                {
                    Status = "failed",
                    Error = new OpenAiVideoError
                    {
                        Message = $"OpenAI API error ({response.StatusCode}): {responseBody}"
                    }
                };
            }

            var result = JsonSerializer.Deserialize<OpenAiVideoCreateResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("OpenAI video job created: {Id}", result?.Id);
            return result ?? new OpenAiVideoCreateResponse { Status = "failed" };
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("OpenAI video creation timed out");
            return new OpenAiVideoCreateResponse
            {
                Status = "failed",
                Error = new OpenAiVideoError { Message = "Request timed out" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI video creation exception");
            return new OpenAiVideoCreateResponse
            {
                Status = "failed",
                Error = new OpenAiVideoError { Message = ex.Message }
            };
        }
    }

    /// <summary>
    /// Retrieve video job status
    /// </summary>
    public async Task<OpenAiVideoStatusResponse> RetrieveVideoAsync(string videoId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving OpenAI video status: {VideoId}", videoId);

        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            return new OpenAiVideoStatusResponse
            {
                Id = videoId,
                Status = "failed",
                Error = new OpenAiVideoError { Message = "OpenAI API key not configured" }
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/videos/{videoId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI video status failed: {Status} - {Body}", 
                    response.StatusCode, responseBody);
                
                return new OpenAiVideoStatusResponse
                {
                    Id = videoId,
                    Status = "failed",
                    Error = new OpenAiVideoError
                    {
                        Message = $"OpenAI API error ({response.StatusCode}): {responseBody}"
                    }
                };
            }

            var result = JsonSerializer.Deserialize<OpenAiVideoStatusResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogDebug("OpenAI video {VideoId} status: {Status}, progress: {Progress}%", 
                videoId, result?.Status, result?.Progress);
            
            return result ?? new OpenAiVideoStatusResponse { Id = videoId, Status = "unknown" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI video status exception for {VideoId}", videoId);
            return new OpenAiVideoStatusResponse
            {
                Id = videoId,
                Status = "error",
                Error = new OpenAiVideoError { Message = ex.Message }
            };
        }
    }

    /// <summary>
    /// Download video content as bytes
    /// </summary>
    public async Task<byte[]?> DownloadVideoContentAsync(string videoId, CancellationToken ct = default)
    {
        _logger.LogInformation("Downloading OpenAI video content: {VideoId}", videoId);

        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Cannot download video: OpenAI API key not configured");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/videos/{videoId}/content");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenAI video download failed: {Status} - {Body}", 
                    response.StatusCode, errorBody);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            _logger.LogInformation("Downloaded OpenAI video: {VideoId}, size: {Size} bytes", 
                videoId, bytes.Length);
            
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI video download exception for {VideoId}", videoId);
            return null;
        }
    }

    /// <summary>
    /// Check if API key is configured (checks database first, then appsettings)
    /// </summary>
    public bool IsConfigured => IsConfiguredAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Check if API key is configured (async version)
    /// </summary>
    public async Task<bool> IsConfiguredAsync()
    {
        // Check database first
        var isDbConfigured = await _configService.IsConfiguredAsync();
        if (isDbConfigured)
        {
            return true;
        }
        
        // Fall back to appsettings
        return !string.IsNullOrWhiteSpace(_options.ApiKey);
    }
}

