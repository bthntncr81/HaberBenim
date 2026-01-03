using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services.Templates;

/// <summary>
/// Result of template selection
/// </summary>
public class TemplateSelectionResult
{
    public bool Success { get; set; }
    public PublishTemplate? Template { get; set; }
    public string? Format { get; set; }
    public ContentMediaType MediaType { get; set; }
    public string? Error { get; set; }
    public string? SkipReason { get; set; }

    public static TemplateSelectionResult Selected(PublishTemplate template, string format, ContentMediaType mediaType)
        => new() { Success = true, Template = template, Format = format, MediaType = mediaType };

    public static TemplateSelectionResult Skipped(string reason, ContentMediaType mediaType)
        => new() { Success = false, SkipReason = reason, MediaType = mediaType };

    public static TemplateSelectionResult Failed(string error)
        => new() { Success = false, Error = error };
}

/// <summary>
/// Selects the appropriate template for a content item and platform
/// </summary>
public interface ITemplateSelector
{
    Task<TemplateSelectionResult> SelectTemplateAsync(
        Guid contentItemId,
        string platform,
        CancellationToken ct = default);

    Task<TemplateSelectionResult> SelectTemplateAsync(
        ContentItem content,
        string platform,
        CancellationToken ct = default);

    string DetermineFormat(string platform, ContentMediaType mediaType, ContentItem content);
}

public class TemplateSelector : ITemplateSelector
{
    private readonly AppDbContext _db;
    private readonly ITemplateRuleEvaluator _ruleEvaluator;
    private readonly ILogger<TemplateSelector> _logger;

    public TemplateSelector(
        AppDbContext db,
        ITemplateRuleEvaluator ruleEvaluator,
        ILogger<TemplateSelector> logger)
    {
        _db = db;
        _ruleEvaluator = ruleEvaluator;
        _logger = logger;
    }

    public async Task<TemplateSelectionResult> SelectTemplateAsync(
        Guid contentItemId,
        string platform,
        CancellationToken ct = default)
    {
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.MediaLinks)
                .ThenInclude(m => m.MediaAsset)
            .Include(c => c.Media)
            .FirstOrDefaultAsync(c => c.Id == contentItemId, ct);

        if (content == null)
        {
            return TemplateSelectionResult.Failed("Content item not found");
        }

        return await SelectTemplateAsync(content, platform, ct);
    }

    public async Task<TemplateSelectionResult> SelectTemplateAsync(
        ContentItem content,
        string platform,
        CancellationToken ct = default)
    {
        // 1. Determine media type
        var mediaType = _ruleEvaluator.DetermineMediaType(content);
        
        _logger.LogDebug("Selecting template for content {ContentId}, platform {Platform}, mediaType {MediaType}",
            content.Id, platform, mediaType);

        // 2. Determine format based on platform and media type
        var format = DetermineFormat(platform, mediaType, content);
        
        // Check if this platform/format combination should be skipped
        if (string.IsNullOrEmpty(format))
        {
            var reason = GetSkipReason(platform, mediaType);
            _logger.LogDebug("Skipping template selection: {Reason}", reason);
            return TemplateSelectionResult.Skipped(reason, mediaType);
        }

        // 3. Get assigned templates for this source + platform
        var assignments = await _db.SourceTemplateAssignments
            .Include(a => a.Template)
                .ThenInclude(t => t.Spec)
            .Where(a => 
                a.SourceId == content.SourceId &&
                a.Platform == platform &&
                a.IsActive &&
                a.Template.IsActive)
            .ToListAsync(ct);

        // If no assignments, fall back to any active template for this platform/format
        if (assignments.Count == 0)
        {
            _logger.LogDebug("No source assignments found, falling back to global templates");
            
            var globalTemplate = await _db.PublishTemplates
                .Include(t => t.Spec)
                .Where(t => 
                    t.Platform == platform &&
                    t.Format == format &&
                    t.IsActive)
                .OrderBy(t => t.Priority)
                .FirstOrDefaultAsync(ct);

            if (globalTemplate != null)
            {
                // Check rule
                var rule = _ruleEvaluator.ParseRule(globalTemplate.RuleJson);
                if (_ruleEvaluator.Evaluate(rule, content, mediaType))
                {
                    return TemplateSelectionResult.Selected(globalTemplate, format, mediaType);
                }
            }

            return TemplateSelectionResult.Skipped($"No matching template for {platform}/{format}", mediaType);
        }

        // 4. Filter by format and evaluate rules
        var matchingTemplates = new List<(SourceTemplateAssignment Assignment, PublishTemplate Template, int Priority)>();

        foreach (var assignment in assignments)
        {
            var template = assignment.Template;
            
            // Check format matches
            if (template.Format != format)
                continue;

            // Evaluate rule
            var rule = _ruleEvaluator.ParseRule(template.RuleJson);
            if (!_ruleEvaluator.Evaluate(rule, content, mediaType))
                continue;

            // Calculate effective priority
            var priority = assignment.PriorityOverride ?? template.Priority;
            matchingTemplates.Add((assignment, template, priority));
        }

        if (matchingTemplates.Count == 0)
        {
            return TemplateSelectionResult.Skipped($"No matching template rules for {platform}/{format}", mediaType);
        }

        // 5. Choose highest priority (lowest number = highest priority)
        var selected = matchingTemplates.OrderBy(t => t.Priority).First();
        
        _logger.LogInformation("Selected template {TemplateId} ({TemplateName}) for content {ContentId}, platform {Platform}",
            selected.Template.Id, selected.Template.Name, content.Id, platform);

        return TemplateSelectionResult.Selected(selected.Template, format, mediaType);
    }

    /// <summary>
    /// Determines the appropriate format based on platform and media type
    /// </summary>
    public string DetermineFormat(string platform, ContentMediaType mediaType, ContentItem content)
    {
        switch (platform)
        {
            case "Instagram":
                // Video -> Reels, else Post
                return mediaType == ContentMediaType.Video ? "Reels" : "Post";

            case "YouTube":
                // Only video content (Shorts)
                return mediaType == ContentMediaType.Video ? "Shorts" : "";

            case "TikTok":
                // Only video content
                return mediaType == ContentMediaType.Video ? "Video" : "";

            case "X":
                // Check if source is a person/account (for quote tweets)
                // For now, default to Tweet
                var isPersonSource = content.Source?.Type == "X";
                return isPersonSource ? "QuoteTweet" : "Tweet";

            default:
                return "Post";
        }
    }

    private string GetSkipReason(string platform, ContentMediaType mediaType)
    {
        return platform switch
        {
            "YouTube" when mediaType != ContentMediaType.Video 
                => "YouTube Shorts requires video content",
            "TikTok" when mediaType != ContentMediaType.Video 
                => "TikTok requires video content",
            _ => $"No suitable format for {platform} with {mediaType} content"
        };
    }
}

