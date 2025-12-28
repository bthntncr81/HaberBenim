using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services.XIntegration;

/// <summary>
/// Service for ingesting tweets from X sources
/// </summary>
public class XIngestionService
{
    private readonly AppDbContext _db;
    private readonly XApiClient _apiClient;
    private readonly XOAuthService _oauthService;
    private readonly AlertService _alertService;
    private readonly RuleEngineService _ruleEngine;
    private readonly ILogger<XIngestionService> _logger;
    private readonly XIngestionOptions _options;

    public XIngestionService(
        AppDbContext db,
        XApiClient apiClient,
        XOAuthService oauthService,
        AlertService alertService,
        RuleEngineService ruleEngine,
        ILogger<XIngestionService> logger,
        IOptions<XIngestionOptions> options)
    {
        _db = db;
        _apiClient = apiClient;
        _oauthService = oauthService;
        _alertService = alertService;
        _ruleEngine = ruleEngine;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Poll all active X sources for new tweets
    /// </summary>
    public async Task PollAllSourcesAsync(CancellationToken ct = default)
    {
        // Check for available credentials (Bearer token or OAuth 1.0a)
        var appBearerToken = _oauthService.GetAppBearerToken();
        var oauth1Credentials = _oauthService.GetOAuth1Credentials();

        if (string.IsNullOrEmpty(appBearerToken) && oauth1Credentials == null)
        {
            _logger.LogDebug("X ingestion skipped: no credentials configured (Bearer Token or OAuth 1.0a)");
            return;
        }

        var xSources = await _db.Sources
            .Where(s => s.Type == "X" && s.IsActive)
            .ToListAsync(ct);

        if (xSources.Count == 0)
        {
            _logger.LogDebug("No active X sources to poll");
            return;
        }

        _logger.LogInformation("Polling {Count} X sources using {AuthMethod}", 
            xSources.Count, 
            !string.IsNullOrEmpty(appBearerToken) ? "Bearer Token" : "OAuth 1.0a");

        foreach (var source in xSources)
        {
            try
            {
                await PollSourceAsync(source, appBearerToken, oauth1Credentials, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll X source {SourceId} ({SourceName})", 
                    source.Id, source.Name);
            }
        }
    }

    /// <summary>
    /// Poll a single X source for new tweets
    /// </summary>
    public async Task PollSourceAsync(Source source, string? bearerToken, OAuth1Credentials? oauth1Credentials, CancellationToken ct = default)
    {
        // Get or create XSourceState
        var state = await _db.XSourceStates
            .FirstOrDefaultAsync(s => s.SourceId == source.Id, ct);

        if (state == null)
        {
            state = new XSourceState
            {
                SourceId = source.Id,
                ConsecutiveFailures = 0
            };
            _db.XSourceStates.Add(state);
            await _db.SaveChangesAsync(ct);
        }

        state.LastPolledAtUtc = DateTime.UtcNow;

        try
        {
            // Resolve XUserId if not set
            if (string.IsNullOrEmpty(state.XUserId))
            {
                // Use Identifier field first, then fallback to URL/Name parsing
                var username = !string.IsNullOrWhiteSpace(source.Identifier) 
                    ? source.Identifier.TrimStart('@') 
                    : ExtractUsername(source.Url ?? source.Name);
                    
                if (string.IsNullOrEmpty(username))
                {
                    throw new InvalidOperationException($"Cannot determine X username for source {source.Name}. Please set the Identifier field.");
                }

                XApiResult<XUserByUsernameResponse> userResult;
                
                if (!string.IsNullOrEmpty(bearerToken))
                {
                    userResult = await _apiClient.GetUserByUsernameAsync(username, bearerToken);
                }
                else if (oauth1Credentials != null)
                {
                    userResult = await _apiClient.GetUserByUsernameOAuth1Async(username, oauth1Credentials);
                }
                else
                {
                    throw new InvalidOperationException("No credentials available to resolve X user");
                }

                if (!userResult.IsSuccess || userResult.Data?.Data == null)
                {
                    throw new InvalidOperationException($"Failed to resolve X user @{username}: {userResult.Error}");
                }

                state.XUserId = userResult.Data.Data.Id;
                _logger.LogInformation("Resolved X user @{Username} to ID {UserId}", username, state.XUserId);
            }

            // Verify XUserId is set
            if (string.IsNullOrEmpty(state.XUserId))
            {
                throw new InvalidOperationException($"XUserId could not be resolved for source {source.Name}");
            }

            // Fetch tweets - prefer Bearer Token for read operations (higher rate limits)
            XApiResult<XTweetsResponse> tweetsResult;
            
            if (!string.IsNullOrEmpty(bearerToken))
            {
                tweetsResult = await _apiClient.GetUserTweetsAsync(
                    state.XUserId!,
                    bearerToken,
                    maxResults: _options.MaxResultsPerRequest,
                    sinceId: state.LastSinceId);
            }
            else if (oauth1Credentials != null)
            {
                tweetsResult = await _apiClient.GetUserTweetsOAuth1Async(
                    state.XUserId!,
                    oauth1Credentials,
                    maxResults: _options.MaxResultsPerRequest,
                    sinceId: state.LastSinceId);
            }
            else
            {
                throw new InvalidOperationException("No credentials available to fetch tweets");
            }

            if (tweetsResult.IsRateLimited)
            {
                _logger.LogWarning("X API rate limited for source {SourceName}. Reset at: {ResetAt}",
                    source.Name, tweetsResult.RateLimitResetAt);
                
                // Don't count rate limits as failures
                await _db.SaveChangesAsync(ct);
                return;
            }

            if (!tweetsResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to fetch tweets: {tweetsResult.Error}");
            }

            // Process tweets
            var tweets = tweetsResult.Data?.Data ?? [];
            var meta = tweetsResult.Data?.Meta;

            if (tweets.Count > 0)
            {
                _logger.LogInformation("Fetched {Count} tweets from @{Username}", 
                    tweets.Count, source.Identifier ?? source.Name);

                foreach (var tweet in tweets.OrderBy(t => t.Id)) // Process oldest first
                {
                    await ProcessTweetAsync(source, tweet, ct);
                }

                // Update since_id to newest tweet
                if (!string.IsNullOrEmpty(meta?.NewestId))
                {
                    state.LastSinceId = meta.NewestId;
                }
            }

            // Update success status
            state.LastSuccessAtUtc = DateTime.UtcNow;
            state.ConsecutiveFailures = 0;
            state.LastError = null;

            source.LastFetchedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            state.LastFailureAtUtc = DateTime.UtcNow;
            state.ConsecutiveFailures++;
            state.LastError = ex.Message;

            await _db.SaveChangesAsync(ct);

            // Create alert if too many consecutive failures
            if (state.ConsecutiveFailures >= _options.AlertAfterFailures)
            {
                await _alertService.CreateAlertAsync(
                    type: "IngestionDown",
                    severity: "Warn",
                    title: $"X source failing: {source.Name}",
                    message: $"X source '{source.Name}' has failed {state.ConsecutiveFailures} times. Last error: {ex.Message}",
                    meta: new { sourceId = source.Id });
            }

            throw;
        }
    }

    /// <summary>
    /// Process a single tweet and create content item
    /// </summary>
    private async Task ProcessTweetAsync(Source source, XTweetData tweet, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tweet.Id) || string.IsNullOrEmpty(tweet.Text))
        {
            _logger.LogWarning("Skipping invalid tweet data");
            return;
        }

        var externalId = tweet.Id;

        // Check for duplicate
        var exists = await _db.ContentItems
            .AnyAsync(c => c.SourceId == source.Id && c.ExternalId == externalId, ct);

        if (exists)
        {
            _logger.LogDebug("Tweet {TweetId} already exists, skipping", externalId);
            return;
        }

        // Generate dedup hash
        var dedupHash = ComputeDedupHash(tweet.Text);

        // Check for duplicate content
        var duplicateOf = await _db.ContentItems
            .Where(c => c.DedupHash == dedupHash)
            .FirstOrDefaultAsync(ct);

        // Create content item
        var publishedAt = tweet.CreatedAt ?? DateTime.UtcNow;
        var contentItem = new ContentItem
        {
            ExternalId = externalId,
            SourceId = source.Id,
            Title = ExtractTitle(tweet.Text),
            Summary = tweet.Text,
            BodyText = tweet.Text,
            CanonicalUrl = $"https://x.com/{source.Name}/status/{externalId}",
            Language = "tr",
            DedupHash = dedupHash,
            Status = duplicateOf != null ? "Duplicate" : "PendingApproval",
            PublishedAtUtc = publishedAt,
            IngestedAtUtc = DateTime.UtcNow
        };

        _db.ContentItems.Add(contentItem);

        // Create duplicate reference if needed
        if (duplicateOf != null)
        {
            _db.ContentDuplicates.Add(new ContentDuplicate
            {
                ContentItemId = contentItem.Id,
                DuplicateOfContentItemId = duplicateOf.Id,
                Method = "HashMatch",
                DetectedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);

        // Apply rules for non-duplicates
        if (duplicateOf == null)
        {
            var decision = await _ruleEngine.EvaluateAsync(contentItem, source);
            _ruleEngine.ApplyDecision(contentItem, source, decision);
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Ingested tweet {TweetId} as content {ContentId}", 
            externalId, contentItem.Id);
    }

    #region Helpers

    private static string ExtractUsername(string input)
    {
        // Handle various formats: @username, https://x.com/username, username
        var cleaned = input.Trim()
            .Replace("https://twitter.com/", "")
            .Replace("https://x.com/", "")
            .Replace("http://twitter.com/", "")
            .Replace("http://x.com/", "")
            .TrimStart('@');

        // Take only the username part (before any / or ?)
        var parts = cleaned.Split(new[] { '/', '?' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : cleaned;
    }

    private static string ExtractTitle(string text)
    {
        // Take first line or first 100 chars as title
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0 ? lines[0] : text;
        
        if (firstLine.Length > 100)
        {
            return firstLine[..97] + "...";
        }
        
        return firstLine;
    }

    private static string ComputeDedupHash(string text)
    {
        // Normalize text for dedup
        var normalized = text.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("\n", "")
            .Replace("\r", "");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    #endregion
}

