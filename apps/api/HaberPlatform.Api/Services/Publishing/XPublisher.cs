using System.Text.Json;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Services.XIntegration;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Publisher for X (Twitter) - supports real API integration via OAuth2
/// </summary>
public class XPublisher : IChannelPublisher
{
    private readonly ILogger<XPublisher> _logger;
    private readonly PublishingOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public string ChannelName => PublishChannels.X;

    public XPublisher(
        ILogger<XPublisher> logger, 
        IOptions<PublishingOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task<PublishResult> PublishAsync(ContentItem item, ContentDraft draft, CancellationToken ct = default)
    {
        try
        {
            var xText = draft.XText ?? item.Title;
            if (xText.Length > 280)
            {
                xText = xText[..277] + "...";
            }

            // Add hashtags if present
            if (!string.IsNullOrEmpty(draft.HashtagsCsv))
            {
                var hashtags = string.Join(" ", 
                    draft.HashtagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(h => h.Trim().StartsWith('#') ? h.Trim() : $"#{h.Trim()}"));
                
                var combined = $"{xText}\n\n{hashtags}";
                if (combined.Length <= 280)
                {
                    xText = combined;
                }
            }

            var requestPayload = new
            {
                contentItemId = item.Id,
                text = xText,
                hashtags = draft.HashtagsCsv,
                mentions = draft.MentionsCsv
            };

            var requestJson = JsonSerializer.Serialize(requestPayload);

            // Check if real connector is enabled
            if (!_options.X.Enabled)
            {
                // Stub mode
                var stubResponse = new
                {
                    status = "stub",
                    stub = true,
                    connectorEnabled = false,
                    message = "X connector is disabled (stub mode)",
                    tweetId = (string?)null,
                    text = xText,
                    charCount = xText.Length,
                    timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("X post for content {ContentId} - stub mode (connector disabled)", item.Id);
                return PublishResult.Succeeded(requestJson, JsonSerializer.Serialize(stubResponse));
            }

            // Real X API integration
            using var scope = _serviceProvider.CreateScope();
            var oauthService = scope.ServiceProvider.GetRequiredService<XOAuthService>();
            var apiClient = scope.ServiceProvider.GetRequiredService<XApiClient>();
            var alertService = scope.ServiceProvider.GetRequiredService<AlertService>();

            // Try OAuth 1.0a first (preferred - tokens don't expire)
            var oauth1Credentials = oauthService.GetOAuth1Credentials();
            if (oauth1Credentials != null)
            {
                return await PostWithOAuth1Async(apiClient, alertService, oauth1Credentials, xText, requestJson, item.Id);
            }

            // Fallback to OAuth 2.0 (user context with token refresh)
            var connectionResult = await oauthService.GetDefaultPublisherAsync();
            if (connectionResult == null)
            {
                _logger.LogWarning("No X credentials configured (OAuth 1.0a or OAuth 2.0), skipping post");
                
                var noConnectionResponse = new
                {
                    status = "skipped",
                    reason = "No X credentials configured. Please set OAuth 1.0a credentials or connect via OAuth 2.0.",
                    text = xText,
                    timestamp = DateTime.UtcNow
                };

                return PublishResult.Skipped("No X credentials configured", 
                    requestJson, JsonSerializer.Serialize(noConnectionResponse));
            }

            var (connection, accessToken) = connectionResult.Value;

            // Post tweet using OAuth 2.0
            var result = await apiClient.PostTweetAsync(accessToken, xText);

            if (result.IsRateLimited)
            {
                _logger.LogWarning("X API rate limited when posting for content {ContentId}", item.Id);

                var rateLimitResponse = new
                {
                    status = "rate_limited",
                    resetAt = result.RateLimitResetAt,
                    text = xText,
                    timestamp = DateTime.UtcNow
                };

                // Return as retryable failure
                return PublishResult.Failed(
                    $"Rate limited. Retry after: {result.RateLimitResetAt}",
                    requestJson,
                    JsonSerializer.Serialize(rateLimitResponse),
                    isRetryable: true,
                    retryAfter: result.RateLimitResetAt ?? DateTime.UtcNow.AddMinutes(1));
            }

            if (result.IsAuthError)
            {
                _logger.LogError("X auth error when posting for content {ContentId}", item.Id);

                // Create admin alert
                await alertService.CreateAlertAsync(
                    type: "XAuthFailed",
                    severity: "Critical",
                    title: "X authentication failed - reconnect needed",
                    message: $"Failed to post tweet for content {item.Id}. The X connection may have expired. Please reconnect.",
                    meta: new { 
                        contentItemId = item.Id, 
                        connectionId = connection.Id,
                        username = connection.XUsername 
                    });

                return PublishResult.Failed(
                    "X authentication failed - reconnect needed",
                    requestJson,
                    result.RawJson);
            }

            if (!result.IsSuccess || result.Data?.Data == null)
            {
                _logger.LogError("Failed to post tweet for content {ContentId}: {Error}", 
                    item.Id, result.Error);

                return PublishResult.Failed(
                    result.Error ?? "Unknown error posting tweet",
                    requestJson,
                    result.RawJson);
            }

            // Success!
            var tweetData = result.Data.Data;
            var successResponse = new
            {
                status = "success",
                stub = false,
                connectorEnabled = true,
                authMethod = "OAuth2",
                tweetId = tweetData.Id,
                text = tweetData.Text ?? xText,
                tweetUrl = $"https://x.com/{connection.XUsername}/status/{tweetData.Id}",
                postedBy = connection.XUsername,
                timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Posted tweet {TweetId} for content {ContentId} via @{Username} (OAuth2)",
                tweetData.Id, item.Id, connection.XUsername);

            return PublishResult.Succeeded(
                requestJson, 
                JsonSerializer.Serialize(successResponse),
                externalId: tweetData.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process X post for content {ContentId}", item.Id);
            return PublishResult.Failed(ex.Message);
        }
    }

    private async Task<PublishResult> PostWithOAuth1Async(
        XApiClient apiClient,
        AlertService alertService,
        OAuth1Credentials credentials,
        string xText,
        string requestJson,
        Guid contentItemId)
    {
        _logger.LogInformation("Posting tweet using OAuth 1.0a for content {ContentId}", contentItemId);

        var result = await apiClient.PostTweetOAuth1Async(credentials, xText);

        if (result.IsRateLimited)
        {
            _logger.LogWarning("X API rate limited when posting for content {ContentId}", contentItemId);

            var rateLimitResponse = new
            {
                status = "rate_limited",
                resetAt = result.RateLimitResetAt,
                text = xText,
                timestamp = DateTime.UtcNow
            };

            return PublishResult.Failed(
                $"Rate limited. Retry after: {result.RateLimitResetAt}",
                requestJson,
                JsonSerializer.Serialize(rateLimitResponse),
                isRetryable: true,
                retryAfter: result.RateLimitResetAt ?? DateTime.UtcNow.AddMinutes(1));
        }

        if (result.IsAuthError)
        {
            _logger.LogError("X OAuth 1.0a auth error when posting for content {ContentId}: {Error}", 
                contentItemId, result.Error);

            await alertService.CreateAlertAsync(
                type: "XAuthFailed",
                severity: "Critical",
                title: "X OAuth 1.0a authentication failed",
                message: $"Failed to post tweet for content {contentItemId}. Please verify your OAuth 1.0a credentials (API Key, API Secret, Access Token, Access Token Secret).",
                meta: new { contentItemId, error = result.Error });

            return PublishResult.Failed(
                "X OAuth 1.0a authentication failed - verify credentials",
                requestJson,
                result.RawJson ?? result.Error);
        }

        if (!result.IsSuccess || result.Data?.Data == null)
        {
            _logger.LogError("Failed to post tweet for content {ContentId}: {Error}", 
                contentItemId, result.Error);

            return PublishResult.Failed(
                result.Error ?? "Unknown error posting tweet",
                requestJson,
                result.RawJson);
        }

        // Success!
        var tweetData = result.Data.Data;
        var successResponse = new
        {
            status = "success",
            stub = false,
            connectorEnabled = true,
            authMethod = "OAuth1.0a",
            tweetId = tweetData.Id,
            text = tweetData.Text ?? xText,
            tweetUrl = $"https://x.com/i/status/{tweetData.Id}",
            timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Posted tweet {TweetId} for content {ContentId} (OAuth 1.0a)",
            tweetData.Id, contentItemId);

        return PublishResult.Succeeded(
            requestJson, 
            JsonSerializer.Serialize(successResponse),
            externalId: tweetData.Id);
    }
}
