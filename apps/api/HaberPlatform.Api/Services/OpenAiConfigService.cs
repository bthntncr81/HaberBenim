using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services;

/// <summary>
/// Service for managing OpenAI API configuration
/// </summary>
public class OpenAiConfigService
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiConfigService> _logger;

    // SystemSetting keys
    private const string OPENAI_API_KEY_ENC = "OPENAI_API_KEY_ENC";
    private const string OPENAI_ORG_ID = "OPENAI_ORG_ID";
    private const string OPENAI_PROJECT_ID = "OPENAI_PROJECT_ID";
    private const string OPENAI_LAST_TEST_AT = "OPENAI_LAST_TEST_AT";
    private const string OPENAI_LAST_TEST_OK = "OPENAI_LAST_TEST_OK";
    private const string OPENAI_LAST_TEST_ERROR = "OPENAI_LAST_TEST_ERROR";
    private const string OPENAI_SORA2_AVAILABLE = "OPENAI_SORA2_AVAILABLE";

    public OpenAiConfigService(
        AppDbContext db,
        IDataProtectionProvider dataProtection,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiConfigService> logger)
    {
        _db = db;
        _protector = dataProtection.CreateProtector("OpenAiApiKey");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get decrypted API key (for use by video services)
    /// </summary>
    public async Task<string?> GetDecryptedApiKeyAsync()
    {
        var encryptedKey = await GetSettingAsync(OPENAI_API_KEY_ENC);
        if (string.IsNullOrEmpty(encryptedKey))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(encryptedKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt OpenAI API key");
            return null;
        }
    }

    /// <summary>
    /// Get Organization ID
    /// </summary>
    public async Task<string?> GetOrgIdAsync()
    {
        return await GetSettingAsync(OPENAI_ORG_ID);
    }

    /// <summary>
    /// Check if API key is configured
    /// </summary>
    public async Task<bool> IsConfiguredAsync()
    {
        var encryptedKey = await GetSettingAsync(OPENAI_API_KEY_ENC);
        return !string.IsNullOrEmpty(encryptedKey);
    }

    /// <summary>
    /// Get current OpenAI configuration status
    /// </summary>
    public async Task<OpenAiKeyStatusResponse> GetStatusAsync()
    {
        var encryptedKey = await GetSettingAsync(OPENAI_API_KEY_ENC);
        var orgId = await GetSettingAsync(OPENAI_ORG_ID);
        var projectId = await GetSettingAsync(OPENAI_PROJECT_ID);
        var lastTestAt = await GetSettingAsync(OPENAI_LAST_TEST_AT);
        var lastTestOk = await GetSettingAsync(OPENAI_LAST_TEST_OK);
        var lastTestError = await GetSettingAsync(OPENAI_LAST_TEST_ERROR);
        var sora2Available = await GetSettingAsync(OPENAI_SORA2_AVAILABLE);

        string? keyLast4 = null;
        if (!string.IsNullOrEmpty(encryptedKey))
        {
            try
            {
                var decrypted = _protector.Unprotect(encryptedKey);
                if (decrypted.Length >= 4)
                {
                    keyLast4 = decrypted[^4..];
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt OpenAI API key for last4");
            }
        }

        return new OpenAiKeyStatusResponse(
            IsConfigured: !string.IsNullOrEmpty(encryptedKey),
            KeyLast4: keyLast4,
            OrgId: orgId,
            ProjectId: projectId,
            LastTestAtUtc: DateTime.TryParse(lastTestAt, out var dt) ? dt : null,
            LastTestOk: bool.TryParse(lastTestOk, out var ok) ? ok : null,
            LastError: lastTestError,
            Sora2Available: bool.TryParse(sora2Available, out var sora) ? sora : null
        );
    }

    /// <summary>
    /// Save OpenAI API key (encrypted)
    /// </summary>
    public async Task<SaveOpenAiKeyResponse> SaveKeyAsync(SaveOpenAiKeyRequest request)
    {
        try
        {
            // Validate API key format
            if (string.IsNullOrWhiteSpace(request.ApiKey) || request.ApiKey.Length < 20)
            {
                return new SaveOpenAiKeyResponse(false, null, "API key is too short");
            }

            // Encrypt the API key
            var encrypted = _protector.Protect(request.ApiKey);

            // Save encrypted key
            await SaveSettingAsync(OPENAI_API_KEY_ENC, encrypted);

            // Save optional org/project IDs
            if (!string.IsNullOrWhiteSpace(request.OrgId))
            {
                await SaveSettingAsync(OPENAI_ORG_ID, request.OrgId);
            }
            else
            {
                await DeleteSettingAsync(OPENAI_ORG_ID);
            }

            if (!string.IsNullOrWhiteSpace(request.ProjectId))
            {
                await SaveSettingAsync(OPENAI_PROJECT_ID, request.ProjectId);
            }
            else
            {
                await DeleteSettingAsync(OPENAI_PROJECT_ID);
            }

            // Clear previous test results
            await DeleteSettingAsync(OPENAI_LAST_TEST_AT);
            await DeleteSettingAsync(OPENAI_LAST_TEST_OK);
            await DeleteSettingAsync(OPENAI_LAST_TEST_ERROR);
            await DeleteSettingAsync(OPENAI_SORA2_AVAILABLE);

            var last4 = request.ApiKey.Length >= 4 ? request.ApiKey[^4..] : null;

            _logger.LogInformation("OpenAI API key saved successfully (last4: {Last4})", last4);

            return new SaveOpenAiKeyResponse(true, last4, "API key saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OpenAI API key");
            return new SaveOpenAiKeyResponse(false, null, "Failed to save API key");
        }
    }

    /// <summary>
    /// Test the stored OpenAI API key by calling /v1/models
    /// </summary>
    public async Task<TestOpenAiResponse> TestConnectionAsync()
    {
        try
        {
            // Get encrypted key
            var encryptedKey = await GetSettingAsync(OPENAI_API_KEY_ENC);
            if (string.IsNullOrEmpty(encryptedKey))
            {
                return new TestOpenAiResponse(false, null, null, "No API key configured");
            }

            // Decrypt key
            string apiKey;
            try
            {
                apiKey = _protector.Unprotect(encryptedKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt OpenAI API key");
                return new TestOpenAiResponse(false, null, null, "Failed to decrypt stored API key");
            }

            // Get optional org ID
            var orgId = await GetSettingAsync(OPENAI_ORG_ID);

            // Make API call to OpenAI
            var client = _httpClientFactory.CreateClient("OpenAiHttp");
            
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/models");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            
            if (!string.IsNullOrEmpty(orgId))
            {
                request.Headers.Add("OpenAI-Organization", orgId);
            }

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Record test time
            var testTime = DateTime.UtcNow;
            await SaveSettingAsync(OPENAI_LAST_TEST_AT, testTime.ToString("O"));

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = GetSafeErrorMessage(response.StatusCode, responseBody);
                
                await SaveSettingAsync(OPENAI_LAST_TEST_OK, "false");
                await SaveSettingAsync(OPENAI_LAST_TEST_ERROR, errorMessage);
                await DeleteSettingAsync(OPENAI_SORA2_AVAILABLE);

                _logger.LogWarning("OpenAI API test failed: {StatusCode} - {Error}", 
                    response.StatusCode, errorMessage);

                return new TestOpenAiResponse(false, null, null, errorMessage);
            }

            // Parse response
            var modelsResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(responseBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var modelCount = modelsResponse?.Data?.Count ?? 0;
            
            // Check for sora-2 model
            var sora2Available = modelsResponse?.Data?.Any(m => 
                m.Id != null && (m.Id.Equals("sora-2", StringComparison.OrdinalIgnoreCase) || 
                                 m.Id.StartsWith("sora-2", StringComparison.OrdinalIgnoreCase))) ?? false;

            // Save test results
            await SaveSettingAsync(OPENAI_LAST_TEST_OK, "true");
            await SaveSettingAsync(OPENAI_SORA2_AVAILABLE, sora2Available.ToString().ToLower());
            await DeleteSettingAsync(OPENAI_LAST_TEST_ERROR);

            _logger.LogInformation("OpenAI API test successful. Models: {ModelCount}, Sora2: {Sora2}", 
                modelCount, sora2Available);

            return new TestOpenAiResponse(true, sora2Available, modelCount, null);
        }
        catch (TaskCanceledException)
        {
            var error = "Request timeout - OpenAI API did not respond in time";
            await SaveSettingAsync(OPENAI_LAST_TEST_AT, DateTime.UtcNow.ToString("O"));
            await SaveSettingAsync(OPENAI_LAST_TEST_OK, "false");
            await SaveSettingAsync(OPENAI_LAST_TEST_ERROR, error);
            
            return new TestOpenAiResponse(false, null, null, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API test failed with exception");
            
            var error = "Connection error - check network connectivity";
            await SaveSettingAsync(OPENAI_LAST_TEST_AT, DateTime.UtcNow.ToString("O"));
            await SaveSettingAsync(OPENAI_LAST_TEST_OK, "false");
            await SaveSettingAsync(OPENAI_LAST_TEST_ERROR, error);
            
            return new TestOpenAiResponse(false, null, null, error);
        }
    }

    /// <summary>
    /// Delete the stored OpenAI API key
    /// </summary>
    public async Task<bool> DeleteKeyAsync()
    {
        await DeleteSettingAsync(OPENAI_API_KEY_ENC);
        await DeleteSettingAsync(OPENAI_ORG_ID);
        await DeleteSettingAsync(OPENAI_PROJECT_ID);
        await DeleteSettingAsync(OPENAI_LAST_TEST_AT);
        await DeleteSettingAsync(OPENAI_LAST_TEST_OK);
        await DeleteSettingAsync(OPENAI_LAST_TEST_ERROR);
        await DeleteSettingAsync(OPENAI_SORA2_AVAILABLE);

        _logger.LogInformation("OpenAI API key deleted");
        return true;
    }

    private async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    private async Task SaveSettingAsync(string key, string value)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
        {
            setting = new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await _db.SaveChangesAsync();
    }

    private async Task DeleteSettingAsync(string key)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting != null)
        {
            _db.SystemSettings.Remove(setting);
            await _db.SaveChangesAsync();
        }
    }

    private static string GetSafeErrorMessage(System.Net.HttpStatusCode statusCode, string responseBody)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Invalid API key (401 Unauthorized)",
            System.Net.HttpStatusCode.Forbidden => "Access denied (403 Forbidden)",
            System.Net.HttpStatusCode.TooManyRequests => "Rate limited (429 Too Many Requests)",
            System.Net.HttpStatusCode.InternalServerError => "OpenAI server error (500)",
            System.Net.HttpStatusCode.ServiceUnavailable => "OpenAI service unavailable (503)",
            _ => $"API error ({(int)statusCode})"
        };
    }
}
