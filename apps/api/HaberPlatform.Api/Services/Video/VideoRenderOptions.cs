namespace HaberPlatform.Api.Services.Video;

public class VideoRenderOptions
{
    /// <summary>
    /// Path to ffmpeg executable. Default uses system PATH.
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";
    
    /// <summary>
    /// Path to ffprobe executable. Default uses system PATH.
    /// </summary>
    public string FfprobePath { get; set; } = "ffprobe";
    
    /// <summary>
    /// Default video output width (vertical format)
    /// </summary>
    public int DefaultWidth { get; set; } = 1080;
    
    /// <summary>
    /// Default video output height (vertical format)
    /// </summary>
    public int DefaultHeight { get; set; } = 1920;
    
    /// <summary>
    /// Default video bitrate (e.g., "4M" for 4 Mbps)
    /// </summary>
    public string VideoBitrate { get; set; } = "4M";
    
    /// <summary>
    /// Default audio bitrate (e.g., "128k")
    /// </summary>
    public string AudioBitrate { get; set; } = "128k";
    
    /// <summary>
    /// Maximum video duration in seconds
    /// </summary>
    public int MaxDurationSeconds { get; set; } = 60;
    
    /// <summary>
    /// Font file path for text overlays
    /// </summary>
    public string? FontPath { get; set; }
    
    /// <summary>
    /// Default font for text overlays
    /// </summary>
    public string DefaultFont { get; set; } = "Arial";
}

