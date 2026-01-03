using System.Text.RegularExpressions;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Templates;

public interface ITemplateVariableResolver
{
    Dictionary<string, string> ResolveVariables(ContentItem content, PublishedContent? published = null);
    string ResolveText(string template, Dictionary<string, string> vars);
    string ResolveTextSpec(string textSpecJson, Dictionary<string, string> vars);
    string TrimToLines(string text, int maxLines);
    string TrimToChars(string text, int maxChars);
}

public class TemplateVariableResolver : ITemplateVariableResolver
{
    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public Dictionary<string, string> ResolveVariables(ContentItem content, PublishedContent? published = null)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Core content fields
        vars["title"] = content.Title ?? "";
        vars["summary"] = content.Summary ?? "";
        vars["body"] = content.BodyText ?? "";
        
        // Source info
        vars["sourceName"] = content.Source?.Name ?? "";
        vars["category"] = content.Source?.Category ?? "";
        vars["sourceUrl"] = content.CanonicalUrl ?? "";
        
        // Published content fields (if available)
        if (published != null)
        {
            vars["webTitle"] = published.WebTitle ?? content.Title ?? "";
            vars["webBody"] = published.WebBody ?? content.BodyText ?? "";
            vars["url"] = $"/haber/{published.Path}";
            vars["slug"] = published.Slug ?? "";
            vars["path"] = published.Path ?? "";
            vars["publishedAt"] = published.PublishedAtUtc.ToString("dd.MM.yyyy HH:mm");
        }
        else
        {
            vars["webTitle"] = content.Title ?? "";
            vars["webBody"] = content.BodyText ?? "";
            vars["url"] = "";
            vars["slug"] = "";
            vars["path"] = "";
            vars["publishedAt"] = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm");
        }
        
        // Draft fields (if available)
        if (content.Draft != null)
        {
            vars["xText"] = content.Draft.XText ?? "";
            vars["hashtags"] = FormatHashtags(content.Draft.HashtagsCsv);
            vars["mentions"] = content.Draft.MentionsCsv ?? "";
            vars["mobileSummary"] = content.Draft.MobileSummary ?? "";
            vars["pushTitle"] = content.Draft.PushTitle ?? "";
            vars["pushBody"] = content.Draft.PushBody ?? "";
        }
        else
        {
            vars["xText"] = "";
            vars["hashtags"] = "";
            vars["mentions"] = "";
            vars["mobileSummary"] = "";
            vars["pushTitle"] = "";
            vars["pushBody"] = "";
        }

        // Breaking news
        vars["isBreaking"] = content.IsBreaking ? "SON DAKİKA" : "";
        vars["breakingNote"] = content.BreakingNote ?? "";

        // Primary image path - try multiple sources
        var primaryImagePath = "";
        
        // 1. From published content
        if (published != null && !string.IsNullOrEmpty(published.PrimaryImageUrl))
        {
            primaryImagePath = published.PrimaryImageUrl;
        }
        // 2. From ContentMediaLink (local MediaAsset)
        else if (content.MediaLinks?.Any() == true)
        {
            var primaryLink = content.MediaLinks.FirstOrDefault(m => m.IsPrimary) 
                ?? content.MediaLinks.FirstOrDefault();
            if (primaryLink?.MediaAsset != null)
            {
                primaryImagePath = $"/media/{primaryLink.MediaAsset.StoragePath}";
            }
        }
        // 3. From ContentMedia (external URL - needs to be downloaded or used directly)
        else if (content.Media?.Any() == true)
        {
            var primaryMedia = content.Media.FirstOrDefault(m => m.MediaType == "image");
            if (primaryMedia != null)
            {
                // For external URLs, we'll pass the URL directly
                primaryImagePath = primaryMedia.Url;
            }
        }
        
        vars["primaryImagePath"] = primaryImagePath;
        vars["primaryImageUrl"] = primaryImagePath; // Alias

        return vars;
    }

    public string ResolveText(string template, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template))
            return "";

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return vars.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    public string ResolveTextSpec(string textSpecJson, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(textSpecJson))
            return "{}";

        // Resolve placeholders in the entire JSON string
        // This works because all placeholder values should be JSON-safe (no special chars in news content)
        var resolved = PlaceholderRegex.Replace(textSpecJson, match =>
        {
            var key = match.Groups[1].Value;
            if (vars.TryGetValue(key, out var value))
            {
                // Escape special characters for JSON
                return EscapeJsonString(value);
            }
            return match.Value;
        });

        return resolved;
    }

    private static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    public string TrimToLines(string text, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0)
            return text;

        var lines = text.Split('\n');
        if (lines.Length <= maxLines)
            return text;

        return string.Join('\n', lines.Take(maxLines));
    }

    public string TrimToChars(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || maxChars <= 0)
            return text;

        if (text.Length <= maxChars)
            return text;

        // Try to cut at word boundary
        var cutPoint = text.LastIndexOf(' ', maxChars - 3);
        if (cutPoint < maxChars / 2)
            cutPoint = maxChars - 3;

        return text[..cutPoint].TrimEnd() + "...";
    }

    private static string FormatHashtags(string? hashtagsCsv)
    {
        if (string.IsNullOrEmpty(hashtagsCsv))
            return "";

        var tags = hashtagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", tags.Select(t => t.StartsWith('#') ? t : $"#{t}"));
    }
}

