using System.Text.Json;
using System.Text.Json.Serialization;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Templates;

/// <summary>
/// Template rule DSL model
/// </summary>
public class TemplateRule
{
    /// <summary>Filter by media type: ["image","video","text"]</summary>
    [JsonPropertyName("mediaType")]
    public List<string>? MediaType { get; set; }
    
    /// <summary>Filter by categories: ["Gundem","Spor","Finans"]</summary>
    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }
    
    /// <summary>Filter by breaking status: true/false/null (null = any)</summary>
    [JsonPropertyName("breaking")]
    public bool? Breaking { get; set; }
    
    /// <summary>Filter by source type: ["RSS","X","Manual"]</summary>
    [JsonPropertyName("sourceType")]
    public List<string>? SourceType { get; set; }
}

/// <summary>
/// Determines the media type of content
/// </summary>
public enum ContentMediaType
{
    Text,
    Image,
    Video
}

/// <summary>
/// Evaluates template rules against content
/// </summary>
public interface ITemplateRuleEvaluator
{
    bool Evaluate(TemplateRule? rule, ContentItem content, ContentMediaType mediaType);
    TemplateRule? ParseRule(string? ruleJson);
    ContentMediaType DetermineMediaType(ContentItem content);
}

public class TemplateRuleEvaluator : ITemplateRuleEvaluator
{
    private readonly ILogger<TemplateRuleEvaluator> _logger;

    public TemplateRuleEvaluator(ILogger<TemplateRuleEvaluator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Evaluates if content matches the template rule
    /// </summary>
    public bool Evaluate(TemplateRule? rule, ContentItem content, ContentMediaType mediaType)
    {
        // No rule = always matches
        if (rule == null)
            return true;

        // Check media type filter
        if (rule.MediaType != null && rule.MediaType.Count > 0)
        {
            var mediaTypeStr = mediaType.ToString().ToLowerInvariant();
            if (!rule.MediaType.Any(m => m.Equals(mediaTypeStr, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Check categories filter
        if (rule.Categories != null && rule.Categories.Count > 0)
        {
            var category = content.Source?.Category ?? "";
            if (!rule.Categories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Check breaking filter
        if (rule.Breaking.HasValue)
        {
            if (content.IsBreaking != rule.Breaking.Value)
            {
                return false;
            }
        }

        // Check source type filter
        if (rule.SourceType != null && rule.SourceType.Count > 0)
        {
            var sourceType = content.Source?.Type ?? "";
            if (!rule.SourceType.Any(t => t.Equals(sourceType, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses a rule JSON string into a TemplateRule object
    /// </summary>
    public TemplateRule? ParseRule(string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TemplateRule>(ruleJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse template rule JSON: {RuleJson}", ruleJson);
            return null;
        }
    }

    /// <summary>
    /// Determines the media type of content based on its media links
    /// </summary>
    public ContentMediaType DetermineMediaType(ContentItem content)
    {
        // Check for video first (highest priority)
        if (content.MediaLinks?.Any(m => m.MediaAsset?.Kind == "Video") == true)
        {
            return ContentMediaType.Video;
        }

        // Check for image
        if (content.MediaLinks?.Any(m => m.MediaAsset?.Kind == "Image") == true)
        {
            return ContentMediaType.Image;
        }

        // Check legacy ContentMedia
        if (content.Media?.Any(m => m.MediaType.Equals("video", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return ContentMediaType.Video;
        }

        if (content.Media?.Any(m => m.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return ContentMediaType.Image;
        }

        // Default to text
        return ContentMediaType.Text;
    }
}

