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

public interface ITemplatePreviewRenderer
{
    Task<TemplatePreviewResult> RenderPreviewAsync(
        PublishTemplate template,
        ContentItem content,
        PublishedContent? published = null,
        CancellationToken ct = default);
}

public class TemplatePreviewResult
{
    public bool Success { get; set; }
    public Guid? AssetId { get; set; }
    public string? PreviewUrl { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string> ResolvedVars { get; set; } = new();

    public static TemplatePreviewResult Succeeded(Guid assetId, string previewUrl, Dictionary<string, string> vars)
        => new() { Success = true, AssetId = assetId, PreviewUrl = previewUrl, ResolvedVars = vars };

    public static TemplatePreviewResult Failed(string error)
        => new() { Success = false, Error = error };
}

public class TemplatePreviewRenderer : ITemplatePreviewRenderer
{
    private readonly AppDbContext _db;
    private readonly ITemplateVariableResolver _resolver;
    private readonly ILogger<TemplatePreviewRenderer> _logger;
    private readonly MediaOptions _mediaOptions;
    private readonly FontFamily _fontFamily;

    public TemplatePreviewRenderer(
        AppDbContext db,
        ITemplateVariableResolver resolver,
        IOptions<MediaOptions> mediaOptions,
        ILogger<TemplatePreviewRenderer> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
        _mediaOptions = mediaOptions.Value;

        // Try to load a system font
        _fontFamily = LoadFontFamily();
    }

    private FontFamily LoadFontFamily()
    {
        // Try system fonts in order of preference
        if (SystemFonts.TryGet("Arial", out var arial))
            return arial;
        if (SystemFonts.TryGet("Helvetica", out var helvetica))
            return helvetica;
        if (SystemFonts.TryGet("DejaVu Sans", out var dejavu))
            return dejavu;
        if (SystemFonts.TryGet("Liberation Sans", out var liberation))
            return liberation;
        
        // Fallback to first available system font
        foreach (var family in SystemFonts.Families)
        {
            return family;
        }
        
        throw new InvalidOperationException("No system fonts available");
    }

    public async Task<TemplatePreviewResult> RenderPreviewAsync(
        PublishTemplate template,
        ContentItem content,
        PublishedContent? published = null,
        CancellationToken ct = default)
    {
        try
        {
            // Load spec
            var spec = await _db.PublishTemplateSpecs
                .FirstOrDefaultAsync(s => s.TemplateId == template.Id, ct);

            if (spec == null || string.IsNullOrEmpty(spec.VisualSpecJson))
            {
                return TemplatePreviewResult.Failed("Template has no visual spec");
            }

            // Parse visual spec
            var visualSpec = JsonSerializer.Deserialize<VisualSpec>(spec.VisualSpecJson);
            if (visualSpec == null)
            {
                return TemplatePreviewResult.Failed("Invalid visual spec JSON");
            }

            // Resolve variables
            var vars = _resolver.ResolveVariables(content, published);

            // Create image
            var width = visualSpec.Canvas.Width;
            var height = visualSpec.Canvas.Height;
            var bgColor = ParseColor(visualSpec.Canvas.Background);

            using var image = new Image<Rgba32>(width, height);
            
            // Fill background
            image.Mutate(ctx => ctx.BackgroundColor(bgColor));

            // Render layers
            foreach (var layer in visualSpec.Layers)
            {
                await RenderLayerAsync(image, layer, vars, ct);
            }

            // Save to storage
            var assetId = Guid.NewGuid();
            var storagePath = $"{assetId}.png";
            var fullPath = Path.Combine(GetAbsoluteRootDir(), storagePath);

            Directory.CreateDirectory(GetAbsoluteRootDir());
            await image.SaveAsPngAsync(fullPath, ct);

            // Create MediaAsset
            var asset = new MediaAsset
            {
                Id = assetId,
                Kind = MediaKinds.Image,
                Origin = "TemplatePreview",
                StoragePath = storagePath,
                ContentType = "image/png",
                SizeBytes = new FileInfo(fullPath).Length,
                Width = width,
                Height = height,
                AltText = $"Preview for {template.Name}",
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.MediaAssets.Add(asset);
            await _db.SaveChangesAsync(ct);

            var previewUrl = $"{_mediaOptions.PublicBasePath}/{storagePath}";

            _logger.LogInformation("Rendered template preview {AssetId} for template {TemplateId}, content {ContentId}",
                assetId, template.Id, content.Id);

            return TemplatePreviewResult.Succeeded(assetId, previewUrl, vars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template preview for template {TemplateId}", template.Id);
            return TemplatePreviewResult.Failed($"Render error: {ex.Message}");
        }
    }

    private async Task RenderLayerAsync(
        Image<Rgba32> image,
        LayerSpec layer,
        Dictionary<string, string> vars,
        CancellationToken ct)
    {
        switch (layer.Type.ToLowerInvariant())
        {
            case "rect":
                RenderRect(image, layer);
                break;
            case "text":
                RenderText(image, layer, vars);
                break;
            case "image":
                await RenderImageAsync(image, layer, vars, ct);
                break;
            case "asset":
                await RenderAssetAsync(image, layer, ct);
                break;
        }
    }

    private void RenderRect(Image<Rgba32> image, LayerSpec layer)
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
        
        image.Mutate(ctx =>
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

    private void RenderText(Image<Rgba32> image, LayerSpec layer, Dictionary<string, string> vars)
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

        // Apply line clamp
        if (layer.LineClamp.HasValue && layer.LineClamp > 0)
        {
            text = ClampLines(text, layer.LineClamp.Value);
        }

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(layer.X, layer.Y),
            WrappingLength = layer.Width,
            HorizontalAlignment = ParseAlignment(layer.Align)
        };

        image.Mutate(ctx => ctx.DrawText(options, text, color));
    }

    private async Task RenderImageAsync(
        Image<Rgba32> canvas,
        LayerSpec layer,
        Dictionary<string, string> vars,
        CancellationToken ct)
    {
        // Try to load primary image from content
        Image<Rgba32>? sourceImage = null;
        
        if (layer.Source == "primaryImage" && vars.TryGetValue("primaryImagePath", out var imagePath) && !string.IsNullOrEmpty(imagePath))
        {
            sourceImage = await LoadImageFromPathOrUrlAsync(imagePath, ct);
        }

        if (sourceImage == null)
        {
            // Draw placeholder with icon
            var placeholderColor = ParseColor("#2a2a3e");
            var borderColor = ParseColor("#3a3a4e");
            
            canvas.Mutate(ctx =>
            {
                ctx.Fill(placeholderColor, new RectangleF(layer.X, layer.Y, layer.Width, layer.Height));
                // Draw border
                ctx.Draw(borderColor, 2f, new RectangleF(layer.X, layer.Y, layer.Width, layer.Height));
            });
            
            // Draw "No Image" text in center
            try
            {
                var font = _fontFamily.CreateFont(20, FontStyle.Regular);
                var textColor = ParseColor("#666666");
                var textOptions = new RichTextOptions(font)
                {
                    Origin = new PointF(layer.X + layer.Width / 2, layer.Y + layer.Height / 2 - 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                canvas.Mutate(ctx => ctx.DrawText(textOptions, "Gorsel Yok", textColor));
            }
            catch
            {
                // Font rendering failed, just leave placeholder
            }
            
            return;
        }

        // Resize and draw the image
        using (sourceImage)
        {
            sourceImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(layer.Width, layer.Height),
                Mode = layer.Fit == "cover" ? ResizeMode.Crop : ResizeMode.Max
            }));

            var opacity = layer.Opacity ?? 1f;
            canvas.Mutate(ctx => ctx.DrawImage(sourceImage, new Point(layer.X, layer.Y), opacity));
        }
    }

    private async Task RenderAssetAsync(Image<Rgba32> canvas, LayerSpec layer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(layer.AssetKey))
            return;

        // Load template asset
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

            var opacity = layer.Opacity ?? 1f;
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

    private static string ClampLines(string text, int maxLines)
    {
        var lines = text.Split('\n');
        if (lines.Length <= maxLines)
            return text;

        var result = string.Join('\n', lines.Take(maxLines));
        
        // Truncate last line if needed
        if (result.Length > 150)
        {
            result = result[..147] + "...";
        }

        return result;
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

    private async Task<Image<Rgba32>?> LoadImageFromPathOrUrlAsync(string imagePath, CancellationToken ct)
    {
        try
        {
            // Check if it's an HTTP(S) URL
            if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "HaberBenim/1.0");
                
                var response = await httpClient.GetAsync(imagePath, ct);
                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    return Image.Load<Rgba32>(imageBytes);
                }
                else
                {
                    _logger.LogWarning("Failed to download image from URL: {Url}, Status: {Status}", 
                        imagePath, response.StatusCode);
                }
            }
            else
            {
                // Local path - handle /media/xxx.jpg format
                var fileName = imagePath.TrimStart('/').Replace("media/", "");
                var fullPath = Path.Combine(GetAbsoluteRootDir(), fileName);
                
                if (File.Exists(fullPath))
                {
                    return await Image.LoadAsync<Rgba32>(fullPath, ct);
                }
                else
                {
                    _logger.LogWarning("Image file not found: {Path}", fullPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load image from: {Path}", imagePath);
        }
        
        return null;
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
