using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaberPlatform.Api.Services.Video;

public interface IVideoRenderer
{
    /// <summary>
    /// Check if FFmpeg is available
    /// </summary>
    Task<bool> CheckFfmpegAvailableAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Render video with template overlays
    /// </summary>
    Task<VideoRenderResult> RenderVideoAsync(
        RenderJob job,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get video info using ffprobe
    /// </summary>
    Task<VideoInfo?> GetVideoInfoAsync(string videoPath, CancellationToken ct = default);
}

public class VideoRenderResult
{
    public bool Success { get; set; }
    public Guid? OutputAssetId { get; set; }
    public string? OutputUrl { get; set; }
    public string? Error { get; set; }
    public int DurationSeconds { get; set; }

    public static VideoRenderResult Succeeded(Guid assetId, string url, int duration)
        => new() { Success = true, OutputAssetId = assetId, OutputUrl = url, DurationSeconds = duration };

    public static VideoRenderResult Failed(string error)
        => new() { Success = false, Error = error };
}

public class VideoInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
    public string? Codec { get; set; }
    public double Fps { get; set; }
}

public class VideoRenderer : IVideoRenderer
{
    private readonly AppDbContext _db;
    private readonly VideoRenderOptions _options;
    private readonly MediaOptions _mediaOptions;
    private readonly ITemplateVariableResolver _resolver;
    private readonly ILogger<VideoRenderer> _logger;

    public VideoRenderer(
        AppDbContext db,
        IOptions<VideoRenderOptions> options,
        IOptions<MediaOptions> mediaOptions,
        ITemplateVariableResolver resolver,
        ILogger<VideoRenderer> logger)
    {
        _db = db;
        _options = options.Value;
        _mediaOptions = mediaOptions.Value;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<bool> CheckFfmpegAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFmpeg not available at {Path}", _options.FfmpegPath);
            return false;
        }
    }

    public async Task<VideoInfo?> GetVideoInfoAsync(string videoPath, CancellationToken ct = default)
    {
        try
        {
            var args = $"-v quiet -print_format json -show_format -show_streams \"{videoPath}\"";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfprobePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
                return null;

            var json = JsonDocument.Parse(output);
            var streams = json.RootElement.GetProperty("streams");
            
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_type", out var codecType) && 
                    codecType.GetString() == "video")
                {
                    var info = new VideoInfo
                    {
                        Width = stream.GetProperty("width").GetInt32(),
                        Height = stream.GetProperty("height").GetInt32(),
                        Codec = stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() : null
                    };

                    if (stream.TryGetProperty("duration", out var duration))
                    {
                        info.Duration = double.Parse(duration.GetString() ?? "0");
                    }
                    else if (json.RootElement.TryGetProperty("format", out var format) &&
                             format.TryGetProperty("duration", out var formatDuration))
                    {
                        info.Duration = double.Parse(formatDuration.GetString() ?? "0");
                    }

                    if (stream.TryGetProperty("r_frame_rate", out var fps))
                    {
                        var parts = fps.GetString()?.Split('/');
                        if (parts?.Length == 2 && int.TryParse(parts[0], out var num) && int.TryParse(parts[1], out var den) && den > 0)
                        {
                            info.Fps = (double)num / den;
                        }
                    }

                    return info;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get video info for {Path}", videoPath);
            return null;
        }
    }

    public async Task<VideoRenderResult> RenderVideoAsync(RenderJob job, CancellationToken ct = default)
    {
        try
        {
            // Update job status
            job.Status = RenderJobStatus.Rendering;
            job.Progress = 0;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Load content
            var content = await _db.ContentItems
                .Include(c => c.Draft)
                .Include(c => c.Source)
                .FirstOrDefaultAsync(c => c.Id == job.ContentItemId, ct);

            if (content == null)
                return VideoRenderResult.Failed("Content not found");

            // Load template
            var template = await _db.PublishTemplates
                .Include(t => t.Spec)
                .FirstOrDefaultAsync(t => t.Id == job.TemplateId, ct);

            if (template?.Spec == null)
                return VideoRenderResult.Failed("Template or spec not found");

            // Get source video
            var sourceVideo = await GetSourceVideoAsync(content, job.SourceVideoAssetId, ct);
            if (sourceVideo == null)
                return VideoRenderResult.Failed("No source video found");

            var sourceVideoPath = Path.Combine(GetAbsoluteRootDir(), sourceVideo.StoragePath);
            if (!File.Exists(sourceVideoPath))
                return VideoRenderResult.Failed($"Source video file not found: {sourceVideo.StoragePath}");

            // Get video info
            var videoInfo = await GetVideoInfoAsync(sourceVideoPath, ct);
            if (videoInfo == null)
                return VideoRenderResult.Failed("Failed to get video info");

            // Parse visual spec
            var visualSpec = JsonSerializer.Deserialize<VisualSpec>(template.Spec.VisualSpecJson ?? "{}");
            if (visualSpec == null)
                return VideoRenderResult.Failed("Invalid visual spec");

            // Resolve variables
            var published = await _db.PublishedContents
                .FirstOrDefaultAsync(p => p.ContentItemId == content.Id, ct);
            var vars = _resolver.ResolveVariables(content, published);

            // Build FFmpeg filter
            var filterComplex = await BuildFilterComplexAsync(visualSpec, vars, sourceVideoPath, videoInfo, ct);

            // Generate output path
            var outputAssetId = Guid.NewGuid();
            var outputPath = Path.Combine(GetAbsoluteRootDir(), $"{outputAssetId}.mp4");

            // Run FFmpeg
            var ffmpegResult = await RunFfmpegAsync(sourceVideoPath, outputPath, filterComplex, job, ct);
            if (!ffmpegResult.Success)
                return VideoRenderResult.Failed(ffmpegResult.Error ?? "FFmpeg failed");

            // Get output file info
            var outputInfo = new FileInfo(outputPath);
            if (!outputInfo.Exists)
                return VideoRenderResult.Failed("Output file not created");

            var outputVideoInfo = await GetVideoInfoAsync(outputPath, ct);

            // Create MediaAsset
            var asset = new MediaAsset
            {
                Id = outputAssetId,
                Kind = MediaKinds.Video,
                Origin = "TemplateRender",
                StoragePath = $"{outputAssetId}.mp4",
                ContentType = "video/mp4",
                SizeBytes = outputInfo.Length,
                Width = outputVideoInfo?.Width ?? _options.DefaultWidth,
                Height = outputVideoInfo?.Height ?? _options.DefaultHeight,
                DurationSeconds = (int)(outputVideoInfo?.Duration ?? 0),
                AltText = $"Rendered {template.Name} for {content.Title}",
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.MediaAssets.Add(asset);

            // Update job
            job.OutputMediaAssetId = outputAssetId;
            job.Status = RenderJobStatus.Completed;
            job.Progress = 100;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.UpdatedAtUtc = DateTime.UtcNow;
            
            await _db.SaveChangesAsync(ct);

            var outputUrl = $"{_mediaOptions.PublicBasePath}/{outputAssetId}.mp4";

            _logger.LogInformation("Rendered video {AssetId} for job {JobId}, content {ContentId}",
                outputAssetId, job.Id, content.Id);

            return VideoRenderResult.Succeeded(outputAssetId, outputUrl, asset.DurationSeconds ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render video for job {JobId}", job.Id);
            
            job.Status = RenderJobStatus.Failed;
            job.Error = ex.Message;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            
            return VideoRenderResult.Failed($"Render error: {ex.Message}");
        }
    }

    private async Task<MediaAsset?> GetSourceVideoAsync(ContentItem content, Guid? specifiedAssetId, CancellationToken ct)
    {
        // If specific asset ID provided, use that
        if (specifiedAssetId.HasValue)
        {
            return await _db.MediaAssets.FindAsync(new object[] { specifiedAssetId.Value }, ct);
        }

        // Look for primary video in content
        var videoLink = await _db.ContentMediaLinks
            .Include(l => l.MediaAsset)
            .Where(l => l.ContentItemId == content.Id && l.MediaAsset.Kind == MediaKinds.Video)
            .OrderByDescending(l => l.IsPrimary)
            .FirstOrDefaultAsync(ct);

        return videoLink?.MediaAsset;
    }

    private async Task<string> BuildFilterComplexAsync(
        VisualSpec spec,
        Dictionary<string, string> vars,
        string sourceVideoPath,
        VideoInfo videoInfo,
        CancellationToken ct)
    {
        var filters = new List<string>();
        var overlayInputs = new List<string>();
        var overlayIndex = 1;

        // Calculate scaling/cropping to target resolution
        var targetWidth = spec.Canvas.Width > 0 ? spec.Canvas.Width : _options.DefaultWidth;
        var targetHeight = spec.Canvas.Height > 0 ? spec.Canvas.Height : _options.DefaultHeight;

        // Scale and crop/pad to target size
        filters.Add($"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease,pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2:color=black");

        // Process layers
        foreach (var layer in spec.Layers)
        {
            var filterPart = await BuildLayerFilterAsync(layer, vars, overlayIndex, ct);
            if (!string.IsNullOrEmpty(filterPart))
            {
                filters.Add(filterPart);
            }
        }

        return string.Join(",", filters);
    }

    private async Task<string?> BuildLayerFilterAsync(
        LayerSpec layer,
        Dictionary<string, string> vars,
        int inputIndex,
        CancellationToken ct)
    {
        switch (layer.Type.ToLowerInvariant())
        {
            case "rect":
                return BuildRectFilter(layer);
            
            case "text":
                return BuildTextFilter(layer, vars);
            
            case "asset":
                return await BuildAssetOverlayFilterAsync(layer, inputIndex, ct);
            
            default:
                return null;
        }
    }

    private string? BuildRectFilter(LayerSpec layer)
    {
        if (string.IsNullOrEmpty(layer.Fill))
            return null;

        var color = layer.Fill.TrimStart('#');
        // drawbox filter for lower-third bands
        return $"drawbox=x={layer.X}:y={layer.Y}:w={layer.Width}:h={layer.Height}:color=0x{color}@0.8:t=fill";
    }

    private string? BuildTextFilter(LayerSpec layer, Dictionary<string, string> vars)
    {
        var text = ResolveBinding(layer.Bind, vars);
        if (string.IsNullOrEmpty(text))
            return null;

        // Escape text for FFmpeg
        text = EscapeTextForFfmpeg(text);

        var fontSize = layer.FontSize ?? 48;
        var fontColor = (layer.Color ?? "#ffffff").TrimStart('#');
        var fontFile = !string.IsNullOrEmpty(_options.FontPath) ? $":fontfile={_options.FontPath}" : "";

        var sb = new StringBuilder();
        sb.Append($"drawtext=text='{text}'");
        sb.Append($":x={layer.X}:y={layer.Y}");
        sb.Append($":fontsize={fontSize}");
        sb.Append($":fontcolor=0x{fontColor}");
        sb.Append(fontFile);
        
        if ((layer.FontWeight ?? 400) >= 700)
        {
            // Bold - use a different font or style if available
        }

        // Handle line clamp by truncating
        if (layer.LineClamp.HasValue && layer.LineClamp > 0)
        {
            // Simple truncation for now - proper line wrapping is complex in FFmpeg
        }

        return sb.ToString();
    }

    private async Task<string?> BuildAssetOverlayFilterAsync(LayerSpec layer, int inputIndex, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(layer.AssetKey))
            return null;

        var asset = await _db.TemplateAssets
            .FirstOrDefaultAsync(a => a.Key == layer.AssetKey, ct);

        if (asset == null)
        {
            _logger.LogWarning("Template asset not found: {AssetKey}", layer.AssetKey);
            return null;
        }

        // For logo overlay, we'll need to use the overlay filter with the logo as an input
        // This returns the filter command for a single overlay
        // Note: Complex overlays require multiple inputs to FFmpeg
        var assetPath = Path.Combine(GetAbsoluteRootDir(), asset.StoragePath);
        
        // movie filter to load the image and overlay it
        return $"movie={EscapePathForFfmpeg(assetPath)},scale={layer.Width}:{layer.Height}[logo];[in][logo]overlay={layer.X}:{layer.Y}";
    }

    private async Task<(bool Success, string? Error)> RunFfmpegAsync(
        string inputPath,
        string outputPath,
        string filterComplex,
        RenderJob job,
        CancellationToken ct)
    {
        try
        {
            var args = new StringBuilder();
            args.Append($"-i \"{inputPath}\" ");
            args.Append($"-vf \"{filterComplex}\" ");
            args.Append($"-c:v libx264 -preset fast -crf 23 ");
            args.Append($"-c:a aac -b:a {_options.AudioBitrate} ");
            args.Append($"-b:v {_options.VideoBitrate} ");
            args.Append($"-movflags +faststart ");
            args.Append($"-t {_options.MaxDurationSeconds} ");
            args.Append($"-y \"{outputPath}\"");

            _logger.LogInformation("Running FFmpeg: {Args}", args.ToString());

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfmpegPath,
                    Arguments = args.ToString(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var errorOutput = new StringBuilder();
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorOutput.AppendLine(e.Data);
                    
                    // Parse progress from FFmpeg output
                    if (e.Data.Contains("time="))
                    {
                        // Update progress (simplified)
                        job.Progress = Math.Min(99, job.Progress + 10);
                    }
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError("FFmpeg failed with exit code {Code}: {Error}", 
                    process.ExitCode, errorOutput.ToString());
                return (false, $"FFmpeg failed: {errorOutput}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private string ResolveBinding(string? binding, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(binding))
            return "";
        return _resolver.ResolveText(binding, vars);
    }

    private static string EscapeTextForFfmpeg(string text)
    {
        // Escape special characters for FFmpeg drawtext
        return text
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    private static string EscapePathForFfmpeg(string path)
    {
        return path.Replace("\\", "/").Replace(":", "\\:");
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

