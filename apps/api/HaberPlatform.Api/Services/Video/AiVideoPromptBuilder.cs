using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Video;

/// <summary>
/// Builds prompts for AI video generation
/// </summary>
public class AiVideoPromptBuilder
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiVideoPromptBuilder> _logger;

    public AiVideoPromptBuilder(IConfiguration configuration, ILogger<AiVideoPromptBuilder> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Build a prompt for AI video generation based on content item
    /// </summary>
    public string BuildPrompt(ContentItem item, ContentDraft? draft, Source? source, string seconds = "8", string size = "1280x720")
    {
        var category = source?.Category ?? "Genel";
        var sourceName = source?.Name ?? "Haber Kaynağı";
        var title = draft?.WebTitle ?? item.Title ?? "Haber";
        var summary = item.Summary ?? "";
        var body = draft?.WebBody ?? "";
        
        // Build script from content
        var script = BuildScript(title, summary, body, category);
        
        // Get template from config or use default
        var template = _configuration["AIVideo:PromptTemplate"] ?? GetDefaultTemplate();
        
        // Determine orientation from size
        var orientation = size.StartsWith("1280") || size.StartsWith("1792") ? "landscape" : "portrait";
        
        // Replace placeholders
        var prompt = template
            .Replace("{script}", script)
            .Replace("{title}", title)
            .Replace("{summary}", TruncateText(summary, 200))
            .Replace("{category}", category)
            .Replace("{sourceName}", sourceName)
            .Replace("{seconds}", seconds)
            .Replace("{size}", size)
            .Replace("{orientation}", orientation);

        _logger.LogDebug("Built AI video prompt for content {Id}: {Prompt}", item.Id, TruncateText(prompt, 100));
        
        return prompt;
    }

    /// <summary>
    /// Build a narration script from content
    /// </summary>
    private string BuildScript(string title, string summary, string body, string category)
    {
        var facts = ExtractKeyFacts(summary, body);
        
        var scriptBuilder = new System.Text.StringBuilder();
        scriptBuilder.Append($"Başlık: {title}. ");
        
        if (!string.IsNullOrWhiteSpace(category) && category != "Genel")
        {
            scriptBuilder.Append($"Kategori: {category}. ");
        }
        
        if (facts.Count > 0)
        {
            scriptBuilder.Append("Önemli noktalar: ");
            for (int i = 0; i < Math.Min(facts.Count, 3); i++)
            {
                scriptBuilder.Append($"{i + 1}. {facts[i]}. ");
            }
        }
        else if (!string.IsNullOrWhiteSpace(summary))
        {
            scriptBuilder.Append($"Özet: {TruncateText(summary, 150)}");
        }

        return scriptBuilder.ToString();
    }

    /// <summary>
    /// Extract key facts from content (simple sentence splitting)
    /// </summary>
    private List<string> ExtractKeyFacts(string summary, string body)
    {
        var facts = new List<string>();
        var text = !string.IsNullOrWhiteSpace(summary) ? summary : body;
        
        if (string.IsNullOrWhiteSpace(text))
            return facts;

        // Simple sentence splitting
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 20 && s.Length < 150)
            .Take(5)
            .ToList();

        return sentences;
    }

    /// <summary>
    /// Get preview of what prompt would be generated
    /// </summary>
    public string GetPromptPreview(ContentItem item, ContentDraft? draft, Source? source, 
        string? customPrompt = null, string seconds = "8", string size = "1280x720")
    {
        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            // Custom prompt still needs safety additions
            return AddSafetyClauses(customPrompt);
        }
        
        return BuildPrompt(item, draft, source, seconds, size);
    }

    /// <summary>
    /// Add safety clauses to any prompt
    /// </summary>
    private string AddSafetyClauses(string prompt)
    {
        const string safetyClauses = " No real persons, no faces of real people, no logos, no trademarks, no brand names, fictional virtual presenter only.";
        
        if (!prompt.Contains("no real persons", StringComparison.OrdinalIgnoreCase))
        {
            prompt += safetyClauses;
        }
        
        return prompt;
    }

    private string GetDefaultTemplate()
    {
        return @"
{seconds}-second studio news clip, {orientation} {size}. 
A fictional virtual Turkish news anchor presents the story in Turkish language.
Clean modern studio background, neutral colors, no logos, no channel branding.
Narration script: {script}
On-screen lower-third text graphic: '{category} | {sourceName}'.
Visuals: subtle animated abstract graphics, no real persons, no photographs of people.
Style: professional newsroom broadcast quality.
".Trim();
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            return text;
        
        return text[..maxLength].TrimEnd() + "...";
    }
}

