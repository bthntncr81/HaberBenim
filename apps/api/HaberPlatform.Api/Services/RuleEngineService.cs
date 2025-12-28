using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services;

public class RuleEngineService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RuleEngineService> _logger;

    public RuleEngineService(AppDbContext db, ILogger<RuleEngineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a content item against all enabled rules and returns the decision.
    /// </summary>
    public async Task<RuleDecisionResult> EvaluateAsync(ContentItem item, Source source)
    {
        // Load enabled rules ordered by priority descending (higher priority wins)
        var rules = await _db.Rules
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

        // Combine text for keyword matching
        var combinedText = $"{item.Title} {item.Summary ?? ""} {item.BodyText}".ToLowerInvariant();

        foreach (var rule in rules)
        {
            if (RuleMatches(rule, item, source, combinedText))
            {
                var result = new RuleDecisionResult
                {
                    DecisionType = rule.DecisionType,
                    Status = DecisionTypeToStatus(rule.DecisionType),
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Reason = $"Matched rule: {rule.Name}"
                };

                // If schedule, set default scheduled time
                if (rule.DecisionType == DecisionTypes.Schedule)
                {
                    result.ScheduledAtUtc = DateTime.UtcNow.AddMinutes(15);
                }

                _logger.LogDebug("Content {ContentId} matched rule {RuleName} -> {DecisionType}", 
                    item.Id, rule.Name, rule.DecisionType);
                
                return result;
            }
        }

        // No rule matched - default to RequireApproval
        _logger.LogDebug("Content {ContentId} matched no rules, defaulting to RequireApproval", item.Id);
        
        return new RuleDecisionResult
        {
            DecisionType = DecisionTypes.RequireApproval,
            Status = ContentStatuses.PendingApproval,
            RuleId = null,
            RuleName = null,
            Reason = "Default - no rule matched"
        };
    }

    /// <summary>
    /// Applies the decision result to a content item and updates decision fields.
    /// </summary>
    public void ApplyDecision(ContentItem item, Source source, RuleDecisionResult decision)
    {
        item.DecisionType = decision.DecisionType;
        item.Status = decision.Status;
        item.DecidedByRuleId = decision.RuleId;
        item.DecisionReason = decision.Reason;
        item.DecidedAtUtc = DateTime.UtcNow;
        item.TrustLevelSnapshot = source.TrustLevel;
        item.ScheduledAtUtc = decision.ScheduledAtUtc;
    }

    /// <summary>
    /// Checks if a rule matches the given content item and source.
    /// </summary>
    private bool RuleMatches(Rule rule, ContentItem item, Source source, string combinedTextLower)
    {
        // Check SourceIdsCsv - if provided, sourceId must be included
        if (!string.IsNullOrWhiteSpace(rule.SourceIdsCsv))
        {
            var sourceIds = ParseGuidsCsv(rule.SourceIdsCsv);
            if (!sourceIds.Contains(source.Id))
            {
                return false;
            }
        }
        // Else check GroupIdsCsv - if provided, source group must match
        else if (!string.IsNullOrWhiteSpace(rule.GroupIdsCsv))
        {
            var groups = ParseCsv(rule.GroupIdsCsv);
            if (string.IsNullOrWhiteSpace(source.Group) || 
                !groups.Any(g => g.Equals(source.Group, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Check MinTrustLevel - source must have at least this level
        if (rule.MinTrustLevel.HasValue)
        {
            if (source.TrustLevel < rule.MinTrustLevel.Value)
            {
                return false;
            }
        }

        // Check KeywordsIncludeCsv - at least one keyword must exist in content
        if (!string.IsNullOrWhiteSpace(rule.KeywordsIncludeCsv))
        {
            var includeKeywords = ParseCsv(rule.KeywordsIncludeCsv);
            var hasMatch = includeKeywords.Any(kw => combinedTextLower.Contains(kw.ToLowerInvariant()));
            if (!hasMatch)
            {
                return false;
            }
        }

        // Check KeywordsExcludeCsv - if any exists, rule does NOT match
        if (!string.IsNullOrWhiteSpace(rule.KeywordsExcludeCsv))
        {
            var excludeKeywords = ParseCsv(rule.KeywordsExcludeCsv);
            var hasExclude = excludeKeywords.Any(kw => combinedTextLower.Contains(kw.ToLowerInvariant()));
            if (hasExclude)
            {
                return false;
            }
        }

        // All conditions passed
        return true;
    }

    /// <summary>
    /// Converts decision type to appropriate content status.
    /// </summary>
    private static string DecisionTypeToStatus(string decisionType)
    {
        return decisionType switch
        {
            DecisionTypes.AutoPublish => ContentStatuses.AutoReady,
            DecisionTypes.RequireApproval => ContentStatuses.PendingApproval,
            DecisionTypes.Block => ContentStatuses.Blocked,
            DecisionTypes.Schedule => ContentStatuses.Scheduled,
            _ => ContentStatuses.PendingApproval
        };
    }

    /// <summary>
    /// Parses a CSV string into a list of trimmed, non-empty strings.
    /// </summary>
    private static List<string> ParseCsv(string csv)
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>
    /// Parses a CSV of GUIDs.
    /// </summary>
    private static HashSet<Guid> ParseGuidsCsv(string csv)
    {
        var result = new HashSet<Guid>();
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (Guid.TryParse(part, out var guid))
            {
                result.Add(guid);
            }
        }
        return result;
    }
}

