using System.Text.Json.Serialization;

namespace HaberPlatform.Api.Models;

// ========== VISUAL SPEC MODELS ==========

public class VisualSpec
{
    [JsonPropertyName("canvas")]
    public CanvasSpec Canvas { get; set; } = new();
    
    [JsonPropertyName("layers")]
    public List<LayerSpec> Layers { get; set; } = new();
}

public class CanvasSpec
{
    [JsonPropertyName("w")]
    public int Width { get; set; } = 1080;
    
    [JsonPropertyName("h")]
    public int Height { get; set; } = 1080;
    
    [JsonPropertyName("bg")]
    public string Background { get; set; } = "#0b0f1a";
}

public class LayerSpec
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    /// <summary>rect, text, image, asset</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("x")]
    public int X { get; set; }
    
    [JsonPropertyName("y")]
    public int Y { get; set; }
    
    [JsonPropertyName("w")]
    public int Width { get; set; }
    
    [JsonPropertyName("h")]
    public int Height { get; set; }
    
    // Common properties
    [JsonPropertyName("opacity")]
    public float? Opacity { get; set; }
    
    // Text properties
    [JsonPropertyName("bind")]
    public string? Bind { get; set; }
    
    [JsonPropertyName("fontSize")]
    public int? FontSize { get; set; }
    
    [JsonPropertyName("fontWeight")]
    public int? FontWeight { get; set; }
    
    [JsonPropertyName("color")]
    public string? Color { get; set; }
    
    [JsonPropertyName("align")]
    public string? Align { get; set; }
    
    [JsonPropertyName("lineClamp")]
    public int? LineClamp { get; set; }
    
    // Rect properties
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }
    
    [JsonPropertyName("fillGradient")]
    public GradientSpec? FillGradient { get; set; }
    
    [JsonPropertyName("radius")]
    public int? Radius { get; set; }
    
    // Image properties
    [JsonPropertyName("source")]
    public string? Source { get; set; }
    
    [JsonPropertyName("fit")]
    public string? Fit { get; set; }
    
    // Asset properties
    [JsonPropertyName("assetKey")]
    public string? AssetKey { get; set; }
}

public class GradientSpec
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "linear"; // linear or radial
    
    [JsonPropertyName("angle")]
    public float? Angle { get; set; } // for linear, 0-360
    
    [JsonPropertyName("colors")]
    public List<string> Colors { get; set; } = new();
    
    [JsonPropertyName("stops")]
    public List<float>? Stops { get; set; }
}

// ========== TEXT SPEC MODELS ==========

public class TextSpec
{
    [JsonPropertyName("instagramCaption")]
    public string? InstagramCaption { get; set; }
    
    [JsonPropertyName("xText")]
    public string? XText { get; set; }
    
    [JsonPropertyName("tiktokHook")]
    public string? TiktokHook { get; set; }
    
    [JsonPropertyName("youtubeTitle")]
    public string? YoutubeTitle { get; set; }
    
    [JsonPropertyName("youtubeDescription")]
    public string? YoutubeDescription { get; set; }
}

// ========== API MODELS ==========

public class CreateTemplateRequest
{
    public required string Name { get; set; }
    public required string Platform { get; set; }
    public required string Format { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public string? RuleJson { get; set; }
}

public class UpdateTemplateRequest
{
    public string? Name { get; set; }
    public string? Platform { get; set; }
    public string? Format { get; set; }
    public int? Priority { get; set; }
    public bool? IsActive { get; set; }
    public string? RuleJson { get; set; }
}

public class UpdateTemplateSpecRequest
{
    public string? VisualSpecJson { get; set; }
    public string? TextSpecJson { get; set; }
}

public class TemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Format { get; set; } = "";
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string? RuleJson { get; set; }
    public bool HasSpec { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class TemplateSpecDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string? VisualSpecJson { get; set; }
    public string? TextSpecJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class TemplatePreviewRequest
{
    public Guid ContentItemId { get; set; }
    public string Variant { get; set; } = "image";
}

public class TemplatePreviewResponse
{
    public string PreviewUrl { get; set; } = "";
    public Dictionary<string, string> ResolvedVars { get; set; } = new();
    public TextSpec? ResolvedTextSpec { get; set; }
}

public class TemplateListQuery
{
    public string? Platform { get; set; }
    public string? Format { get; set; }
    public bool? Active { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ========== TEMPLATE ASSET MODELS ==========

public class TemplateAssetDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Url { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateTemplateAssetRequest
{
    public required string Key { get; set; }
}

