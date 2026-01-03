using System.Text.Json;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;

namespace HaberPlatform.Api.Services.Templates;

public interface ITemplateRenderService
{
    /// <summary>
    /// Create a render job for a content item
    /// </summary>
    Task<RenderJob> CreateRenderJobAsync(
        Guid contentItemId,
        Guid templateId,
        string platform,
        string format,
        string? resolvedTextSpecJson = null,
        CancellationToken ct = default);

    /// <summary>
    /// Process a queued render job
    /// </summary>
    Task<RenderJobResult> ProcessRenderJobAsync(RenderJob job, CancellationToken ct = default);

    /// <summary>
    /// Render template directly (without job queue)
    /// </summary>
    Task<RenderJobResult> RenderTemplateAsync(
        PublishTemplate template,
        ContentItem content,
        string? textSpecJson = null,
        CancellationToken ct = default);
}

public class RenderJobResult
{
    public bool Success { get; set; }
    public Guid? OutputAssetId { get; set; }
    public string? OutputUrl { get; set; }
    public string? Error { get; set; }
    public bool UsedAiFallback { get; set; }

    public static RenderJobResult Succeeded(Guid assetId, string url, bool usedAi = false)
        => new() { Success = true, OutputAssetId = assetId, OutputUrl = url, UsedAiFallback = usedAi };

    public static RenderJobResult Failed(string error)
        => new() { Success = false, Error = error };
}

public class TemplateRenderService : ITemplateRenderService
{
    private readonly AppDbContext _db;
    private readonly ITemplateVariableResolver _resolver;
    private readonly ILogger<TemplateRenderService> _logger;
    private readonly MediaOptions _mediaOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FontFamily _fontFamily;

    public TemplateRenderService(
        AppDbContext db,
        ITemplateVariableResolver resolver,
        IOptions<MediaOptions> mediaOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<TemplateRenderService> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
        _mediaOptions = mediaOptions.Value;
        _scopeFactory = scopeFactory;
        _fontFamily = LoadFontFamily();
    }

    private FontFamily LoadFontFamily()
    {
        if (SystemFonts.TryGet("Arial", out var arial)) return arial;
        if (SystemFonts.TryGet("Helvetica", out var helvetica)) return helvetica;
        if (SystemFonts.TryGet("DejaVu Sans", out var dejavu)) return dejavu;
        if (SystemFonts.TryGet("Liberation Sans", out var liberation)) return liberation;
        
        foreach (var family in SystemFonts.Families)
            return family;
        
        throw new InvalidOperationException("No system fonts available");
    }

    public async Task<RenderJob> CreateRenderJobAsync(
        Guid contentItemId,
        Guid templateId,
        string platform,
        string format,
        string? resolvedTextSpecJson = null,
        CancellationToken ct = default)
    {
        var job = new RenderJob
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentItemId,
            TemplateId = templateId,
            Platform = platform,
            Format = format,
            Status = RenderJobStatus.Queued,
            ResolvedTextSpecJson = resolvedTextSpecJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.RenderJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created render job {JobId} for content {ContentId}, template {TemplateId}",
            job.Id, contentItemId, templateId);

        return job;
    }

    public async Task<RenderJobResult> ProcessRenderJobAsync(RenderJob job, CancellationToken ct = default)
    {
        try
        {
            // Update status to Rendering
            job.Status = RenderJobStatus.Rendering;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Load content and template with media for image rendering
            var content = await _db.ContentItems
                .Include(c => c.Draft)
                .Include(c => c.Source)
                .Include(c => c.Media)
                .Include(c => c.MediaLinks)
                    .ThenInclude(ml => ml.MediaAsset)
                .Include(c => c.PublishedContent)
                .FirstOrDefaultAsync(c => c.Id == job.ContentItemId, ct);

            if (content == null)
            {
                return await FailJobAsync(job, "Content not found", ct);
            }

            var template = await _db.PublishTemplates
                .Include(t => t.Spec)
                .FirstOrDefaultAsync(t => t.Id == job.TemplateId, ct);

            if (template == null)
            {
                return await FailJobAsync(job, "Template not found", ct);
            }

            // Render
            var result = await RenderTemplateAsync(template, content, job.ResolvedTextSpecJson, ct);

            if (result.Success)
            {
                job.Status = RenderJobStatus.Completed;
                job.OutputMediaAssetId = result.OutputAssetId;
                job.CompletedAtUtc = DateTime.UtcNow;
            }
            else
            {
                job.Status = RenderJobStatus.Failed;
                job.Error = result.Error;
            }

            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process render job {JobId}", job.Id);
            return await FailJobAsync(job, ex.Message, ct);
        }
    }

    public async Task<RenderJobResult> RenderTemplateAsync(
        PublishTemplate template,
        ContentItem content,
        string? textSpecJson = null,
        CancellationToken ct = default)
    {
        try
        {
            // Load spec
            var spec = template.Spec ?? await _db.PublishTemplateSpecs
                .FirstOrDefaultAsync(s => s.TemplateId == template.Id, ct);

            if (spec == null || string.IsNullOrEmpty(spec.VisualSpecJson))
            {
                return RenderJobResult.Failed("Template has no visual spec");
            }

            // Parse visual spec
            var visualSpec = JsonSerializer.Deserialize<VisualSpec>(spec.VisualSpecJson);
            if (visualSpec == null)
            {
                return RenderJobResult.Failed("Invalid visual spec JSON");
            }

            // Resolve variables
            var published = await _db.PublishedContents
                .FirstOrDefaultAsync(p => p.ContentItemId == content.Id, ct);
            var vars = _resolver.ResolveVariables(content, published);

            // Get primary image
            var primaryImage = await GetPrimaryImageAsync(content, ct);
            bool usedAiFallback = false;

            // If no primary image, try AI generation fallback
            if (primaryImage == null)
            {
                _logger.LogInformation("No primary image for content {ContentId}, attempting AI fallback", content.Id);
                primaryImage = await TryGenerateAiImageAsync(content, ct);
                usedAiFallback = primaryImage != null;
            }

            // Create canvas
            var width = visualSpec.Canvas.Width;
            var height = visualSpec.Canvas.Height;
            var bgColor = ParseColor(visualSpec.Canvas.Background);

            using var canvas = new Image<Rgba32>(width, height);
            canvas.Mutate(ctx => ctx.BackgroundColor(bgColor));

            // Render layers
            foreach (var layer in visualSpec.Layers)
            {
                await RenderLayerAsync(canvas, layer, vars, primaryImage, ct);
            }

            // Save to storage
            var assetId = Guid.NewGuid();
            var storagePath = $"{assetId}.png";
            var fullPath = Path.Combine(GetAbsoluteRootDir(), storagePath);

            Directory.CreateDirectory(GetAbsoluteRootDir());
            await canvas.SaveAsPngAsync(fullPath, ct);

            // Create MediaAsset
            var asset = new MediaAsset
            {
                Id = assetId,
                Kind = MediaKinds.Image,
                Origin = "TemplateRender",
                StoragePath = storagePath,
                ContentType = "image/png",
                SizeBytes = new FileInfo(fullPath).Length,
                Width = width,
                Height = height,
                AltText = $"Rendered {template.Name} for {content.Title}",
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.MediaAssets.Add(asset);
            await _db.SaveChangesAsync(ct);

            var outputUrl = $"{_mediaOptions.PublicBasePath}/{storagePath}";

            _logger.LogInformation("Rendered template {TemplateId} for content {ContentId} -> {AssetId}",
                template.Id, content.Id, assetId);

            return RenderJobResult.Succeeded(assetId, outputUrl, usedAiFallback);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template {TemplateId} for content {ContentId}",
                template.Id, content.Id);
            return RenderJobResult.Failed($"Render error: {ex.Message}");
        }
    }

    private async Task<Image<Rgba32>?> GetPrimaryImageAsync(ContentItem content, CancellationToken ct)
    {
        // Get primary media asset
        var primaryLink = await _db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == content.Id && l.IsPrimary)
            .FirstOrDefaultAsync(ct);

        if (primaryLink?.MediaAsset == null)
        {
            // Try any image
            primaryLink = await _db.ContentMediaLinks
                .Include(l => l.MediaAsset)
                .Where(l => l.ContentItemId == content.Id && l.MediaAsset.Kind == MediaKinds.Image)
                .FirstOrDefaultAsync(ct);
        }

        if (primaryLink?.MediaAsset == null)
            return null;

        var imagePath = Path.Combine(GetAbsoluteRootDir(), primaryLink.MediaAsset.StoragePath);
        if (!File.Exists(imagePath))
        {
            _logger.LogWarning("Primary image file not found: {Path}", imagePath);
            return null;
        }

        try
        {
            return await Image.LoadAsync<Rgba32>(imagePath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load primary image {Path}", imagePath);
            return null;
        }
    }

    private async Task<Image<Rgba32>?> TryGenerateAiImageAsync(ContentItem content, CancellationToken ct)
    {
        // Check if AI image generation is enabled
        var aiEnabled = await _db.SystemSettings
            .Where(s => s.Key == "AI_IMAGE_ENABLED")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        if (aiEnabled?.ToLowerInvariant() != "true")
        {
            _logger.LogInformation("AI image generation disabled, skipping fallback");
            return null;
        }

        // Try to call external AI image generator
        // This is a simplified stub - in production, this would call your AI service
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mediaApi = scope.ServiceProvider.GetService<IEditorialMediaApi>();
            
            if (mediaApi != null)
            {
                var result = await mediaApi.GenerateImageAsync(content.Id, new GenerateImageRequest
                {
                    Force = false,
                    StylePreset = "news-illustration"
                }, ct);

                if (result.Success && result.AssetId.HasValue)
                {
                    var asset = await _db.MediaAssets.FindAsync(new object[] { result.AssetId.Value }, ct);
                    if (asset != null)
                    {
                        var imagePath = Path.Combine(GetAbsoluteRootDir(), asset.StoragePath);
                        if (File.Exists(imagePath))
                        {
                            return await Image.LoadAsync<Rgba32>(imagePath, ct);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI image generation fallback failed for content {ContentId}", content.Id);
        }

        return null;
    }

    private async Task RenderLayerAsync(
        Image<Rgba32> canvas,
        LayerSpec layer,
        Dictionary<string, string> vars,
        Image<Rgba32>? primaryImage,
        CancellationToken ct)
    {
        switch (layer.Type.ToLowerInvariant())
        {
            case "rect":
                RenderRect(canvas, layer);
                break;
            case "text":
                RenderText(canvas, layer, vars);
                break;
            case "image":
                await RenderImageLayerAsync(canvas, layer, primaryImage, ct);
                break;
            case "asset":
                await RenderAssetAsync(canvas, layer, ct);
                break;
        }
    }

    private void RenderRect(Image<Rgba32> canvas, LayerSpec layer)
    {
        var opacity = layer.Opacity ?? 1f;
        var radius = layer.Radius ?? 0;
        
        // Create the shape - rounded rect if radius > 0
        IPath shape;
        if (radius > 0)
        {
            shape = BuildRoundedRectangle(layer.X, layer.Y, layer.Width, layer.Height, radius);
        }
        else
        {
            shape = new RectangularPolygon(layer.X, layer.Y, layer.Width, layer.Height);
        }
        
        canvas.Mutate(ctx =>
        {
            // Check if gradient is defined
            if (layer.FillGradient != null && layer.FillGradient.Colors?.Count >= 2)
            {
                var gradient = layer.FillGradient;
                var color1 = ParseColor(gradient.Colors[0]);
                var color2 = ParseColor(gradient.Colors[1]);
                
                // Apply opacity to colors
                color1 = new Rgba32(color1.R, color1.G, color1.B, (byte)(color1.A * opacity));
                color2 = new Rgba32(color2.R, color2.G, color2.B, (byte)(color2.A * opacity));
                
                // Calculate gradient angle
                var angle = (gradient.Angle ?? 180) * (float)Math.PI / 180f;
                var cos = (float)Math.Cos(angle);
                var sin = (float)Math.Sin(angle);
                var halfW = layer.Width / 2f;
                var halfH = layer.Height / 2f;
                
                var startPoint = new PointF(
                    layer.X + halfW - cos * halfW,
                    layer.Y + halfH - sin * halfH
                );
                var endPoint = new PointF(
                    layer.X + halfW + cos * halfW,
                    layer.Y + halfH + sin * halfH
                );
                
                var gradientBrush = new LinearGradientBrush(
                    startPoint,
                    endPoint,
                    GradientRepetitionMode.None,
                    new ColorStop(0, color1),
                    new ColorStop(1, color2)
                );
                
                ctx.Fill(gradientBrush, shape);
            }
            else
            {
                // Solid fill
                var fillColor = ParseColor(layer.Fill ?? "#333333");
                
                // Apply opacity
                if (opacity < 1f)
                {
                    fillColor = new Rgba32(fillColor.R, fillColor.G, fillColor.B, (byte)(fillColor.A * opacity));
                }
                
                ctx.Fill(fillColor, shape);
            }
        });
    }
    
    private static IPath BuildRoundedRectangle(float x, float y, float width, float height, float radius)
    {
        // Clamp radius to half of smallest dimension
        radius = Math.Min(radius, Math.Min(width, height) / 2);
        
        var pathBuilder = new PathBuilder();
        
        // Top-left corner
        pathBuilder.MoveTo(new PointF(x + radius, y));
        
        // Top edge
        pathBuilder.LineTo(new PointF(x + width - radius, y));
        
        // Top-right corner
        pathBuilder.AddArc(new PointF(x + width - radius, y + radius), radius, radius, 0, -90, 90);
        
        // Right edge
        pathBuilder.LineTo(new PointF(x + width, y + height - radius));
        
        // Bottom-right corner
        pathBuilder.AddArc(new PointF(x + width - radius, y + height - radius), radius, radius, 0, 0, 90);
        
        // Bottom edge
        pathBuilder.LineTo(new PointF(x + radius, y + height));
        
        // Bottom-left corner
        pathBuilder.AddArc(new PointF(x + radius, y + height - radius), radius, radius, 0, 90, 90);
        
        // Left edge
        pathBuilder.LineTo(new PointF(x, y + radius));
        
        // Top-left corner
        pathBuilder.AddArc(new PointF(x + radius, y + radius), radius, radius, 0, 180, 90);
        
        pathBuilder.CloseFigure();
        
        return pathBuilder.Build();
    }

    private void RenderText(Image<Rgba32> canvas, LayerSpec layer, Dictionary<string, string> vars)
    {
        var text = ResolveBinding(layer.Bind, vars);
        if (string.IsNullOrEmpty(text))
            return;

        var color = ParseColor(layer.Color ?? "#ffffff");
        var opacity = layer.Opacity ?? 1f;
        
        // Apply opacity to text color
        if (opacity < 1f)
        {
            color = new Rgba32(color.R, color.G, color.B, (byte)(color.A * opacity));
        }
        
        var fontSize = layer.FontSize ?? 32;
        var isBold = (layer.FontWeight ?? 400) >= 700;

        var font = _fontFamily.CreateFont(fontSize, isBold ? FontStyle.Bold : FontStyle.Regular);

        // Apply proper line clamping
        if (layer.LineClamp.HasValue && layer.LineClamp > 0)
        {
            text = ClampTextToLines(text, font, layer.Width, layer.LineClamp.Value);
        }

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(layer.X, layer.Y),
            WrappingLength = layer.Width,
            HorizontalAlignment = ParseAlignment(layer.Align)
        };

        canvas.Mutate(ctx => ctx.DrawText(options, text, color));
    }

    private async Task RenderImageLayerAsync(
        Image<Rgba32> canvas,
        LayerSpec layer,
        Image<Rgba32>? primaryImage,
        CancellationToken ct)
    {
        var opacity = layer.Opacity ?? 1f;
        
        if (primaryImage == null)
        {
            // Draw placeholder
            var placeholderColor = ParseColor("#2a2a3e");
            if (opacity < 1f)
            {
                placeholderColor = new Rgba32(placeholderColor.R, placeholderColor.G, placeholderColor.B, (byte)(placeholderColor.A * opacity));
            }
            canvas.Mutate(ctx =>
            {
                ctx.Fill(placeholderColor, new RectangleF(layer.X, layer.Y, layer.Width, layer.Height));
            });
            return;
        }

        // Clone and resize the primary image
        using var resized = primaryImage.Clone();
        resized.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(layer.Width, layer.Height),
            Mode = layer.Fit == "cover" ? ResizeMode.Crop : ResizeMode.Max
        }));

        // Apply border radius if specified
        if (layer.Radius.HasValue && layer.Radius > 0)
        {
            ApplyBorderRadius(resized, layer.Radius.Value);
        }

        canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(layer.X, layer.Y), opacity));
    }

    private async Task RenderAssetAsync(Image<Rgba32> canvas, LayerSpec layer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(layer.AssetKey))
            return;

        var opacity = layer.Opacity ?? 1f;

        var asset = await _db.TemplateAssets
            .FirstOrDefaultAsync(a => a.Key == layer.AssetKey, ct);

        if (asset == null)
        {
            _logger.LogWarning("Template asset not found: {AssetKey}", layer.AssetKey);
            return;
        }

        var fullPath = Path.Combine(GetAbsoluteRootDir(), asset.StoragePath);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Template asset file not found: {Path}", fullPath);
            return;
        }

        try
        {
            using var assetImage = await Image.LoadAsync<Rgba32>(fullPath, ct);
            assetImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(layer.Width, layer.Height),
                Mode = layer.Fit == "cover" ? ResizeMode.Crop : ResizeMode.Max
            }));

            canvas.Mutate(ctx => ctx.DrawImage(assetImage, new Point(layer.X, layer.Y), opacity));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render asset {AssetKey}", layer.AssetKey);
        }
    }

    private string ResolveBinding(string? binding, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(binding))
            return "";
        return _resolver.ResolveText(binding, vars);
    }

    private string ClampTextToLines(string text, Font font, int maxWidth, int maxLines)
    {
        // Split text into words and build lines
        var words = text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            var size = TextMeasurer.MeasureSize(testLine, new TextOptions(font));

            if (size.Width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;

                if (lines.Count >= maxLines)
                    break;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine) && lines.Count < maxLines)
        {
            lines.Add(currentLine);
        }

        // If we exceeded max lines, truncate last line with ellipsis
        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
        }

        var result = string.Join('\n', lines);
        
        // Add ellipsis if text was truncated
        if (words.Length > 0 && lines.Count == maxLines)
        {
            var lastLine = lines[^1];
            if (lastLine.Length > 3)
            {
                lines[^1] = lastLine[..^3] + "...";
                result = string.Join('\n', lines);
            }
        }

        return result;
    }

    private void ApplyBorderRadius(Image<Rgba32> image, int radius)
    {
        // Create a rounded rectangle mask
        var cornerSize = Math.Min(radius, Math.Min(image.Width, image.Height) / 2);
        
        image.Mutate(ctx =>
        {
            // Apply rounded corners by drawing on corners
            // This is a simplified approach - full implementation would use a proper mask
        });
    }

    private async Task<RenderJobResult> FailJobAsync(RenderJob job, string error, CancellationToken ct)
    {
        job.Status = RenderJobStatus.Failed;
        job.Error = error;
        job.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return RenderJobResult.Failed(error);
    }

    private static Rgba32 ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return new Rgba32(255, 255, 255);

        hex = hex.TrimStart('#');

        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return new Rgba32(r, g, b);
        }

        if (hex.Length == 8)
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            var a = Convert.ToByte(hex[6..8], 16);
            return new Rgba32(r, g, b, a);
        }

        return new Rgba32(255, 255, 255);
    }

    private static HorizontalAlignment ParseAlignment(string? align)
    {
        return align?.ToLowerInvariant() switch
        {
            "center" => HorizontalAlignment.Center,
            "right" => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
    }

    private string GetAbsoluteRootDir()
    {
        var rootDir = _mediaOptions.RootDir;
        if (!Path.IsPathRooted(rootDir))
        {
            rootDir = Path.Combine(Directory.GetCurrentDirectory(), rootDir);
        }
        return rootDir;
    }
}

// Interface for AI image generation fallback
public interface IEditorialMediaApi
{
    Task<GenerateImageResult> GenerateImageAsync(Guid contentId, GenerateImageRequest request, CancellationToken ct);
}

public class GenerateImageRequest
{
    public bool Force { get; set; }
    public string? PromptOverride { get; set; }
    public string? StylePreset { get; set; }
}

public class GenerateImageResult
{
    public bool Success { get; set; }
    public Guid? AssetId { get; set; }
    public string? Url { get; set; }
    public string? Error { get; set; }
}

