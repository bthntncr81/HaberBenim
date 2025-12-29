namespace HaberPlatform.Api.Services.Media;

/// <summary>
/// Interface for AI image generation providers
/// </summary>
public interface IImageGenerator
{
    /// <summary>
    /// Generate an image based on a text prompt
    /// </summary>
    /// <param name="prompt">Text prompt describing desired image</param>
    /// <param name="width">Desired width in pixels</param>
    /// <param name="height">Desired height in pixels</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Image bytes and content type, or null if generation failed</returns>
    Task<ImageGenerationOutput?> GenerateAsync(
        string prompt, 
        int width, 
        int height, 
        CancellationToken ct = default);

    /// <summary>
    /// Check if the generator is available and configured
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Output from image generation
/// </summary>
public record ImageGenerationOutput(
    byte[] ImageBytes,
    string ContentType,
    int Width,
    int Height
);

