using Microsoft.Extensions.Options;
using HaberPlatform.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace HaberPlatform.Api.Services.Media;

/// <summary>
/// AI image generator using Pollinations.ai external HTTP API
/// </summary>
public class PollinationsImageGenerator : IImageGenerator
{
    private readonly HttpClient _httpClient;
    private readonly AIImageOptions _options;
    private readonly ILogger<PollinationsImageGenerator> _logger;

    public PollinationsImageGenerator(
        IHttpClientFactory httpClientFactory,
        IOptions<AIImageOptions> options,
        ILogger<PollinationsImageGenerator> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Pollinations");
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // Pollinations is an external service, we assume it's available if enabled
        return Task.FromResult(_options.Enabled && 
            _options.Provider.Equals("PollinationsLegacy", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ImageGenerationOutput?> GenerateAsync(
        string prompt,
        int width,
        int height,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("AI image generation is disabled");
            return null;
        }

        var maxRetries = _options.Pollinations.MaxRetries;
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for Pollinations", 
                        attempt, maxRetries);
                    // Wait a bit before retry
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
                }

                var result = await TryGenerateImageAsync(prompt, width, height, ct);
                if (result != null)
                {
                    return result;
                }
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
            {
                lastException = ex;
                _logger.LogWarning("Pollinations attempt {Attempt} timed out after {Timeout}s", 
                    attempt + 1, _options.Pollinations.TimeoutSeconds);
                
                if (attempt >= maxRetries)
                {
                    throw new ExternalImageGeneratorException(
                        $"Request timed out after {_options.Pollinations.TimeoutSeconds}s (tried {attempt + 1} times)", 
                        504);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Pollinations attempt {Attempt} network error", attempt + 1);
                
                if (attempt >= maxRetries)
                {
                    throw new ExternalImageGeneratorException(
                        $"Network error: {ex.Message}", 
                        503);
                }
            }
            catch (ExternalImageGeneratorException ex) when (ex.StatusCode >= 500)
            {
                lastException = ex;
                _logger.LogWarning("Pollinations attempt {Attempt} server error: {Status}", 
                    attempt + 1, ex.StatusCode);
                
                if (attempt >= maxRetries)
                {
                    throw;
                }
            }
            catch (ExternalImageGeneratorException)
            {
                throw; // Don't retry client errors (4xx)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating image from Pollinations");
                throw new ExternalImageGeneratorException($"Unexpected error: {ex.Message}", 500);
            }
        }

        // Should not reach here, but safety
        throw lastException ?? new ExternalImageGeneratorException("Unknown error", 500);
    }

    private async Task<ImageGenerationOutput?> TryGenerateImageAsync(
        string prompt,
        int width,
        int height,
        CancellationToken ct)
    {
        // Build the URL: baseUrl + URL-encoded prompt + query params
        // Use shorter prompt for faster generation
        var baseUrl = _options.Pollinations.BaseUrl.TrimEnd('/');
        var encodedPrompt = Uri.EscapeDataString(prompt);
        // Add seed for reproducibility, nologo to avoid watermarks
        var seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
        var url = $"{baseUrl}/{encodedPrompt}?nologo=true&seed={seed}";

        _logger.LogInformation("Generating image from Pollinations: {Prompt}", 
            prompt.Length > 80 ? prompt[..80] + "..." : prompt);

        // Create request with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.Pollinations.TimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", _options.Pollinations.UserAgent);

        var response = await _httpClient.SendAsync(request, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Pollinations API returned {StatusCode}", response.StatusCode);
            throw new ExternalImageGeneratorException(
                $"Pollinations API returned {response.StatusCode}",
                (int)response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        
        // Validate we got an image, not HTML error page
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Pollinations returned non-image content type: {ContentType}", contentType);
            throw new ExternalImageGeneratorException(
                $"Pollinations returned non-image content: {contentType}",
                500);
        }

        var imageBytes = await response.Content.ReadAsByteArrayAsync(cts.Token);

        _logger.LogInformation("Received {Size} bytes from Pollinations, content-type: {ContentType}",
            imageBytes.Length, contentType);

        // Validate and process image with ImageSharp
        var processedResult = await ProcessAndResizeImageAsync(imageBytes, width, height, ct);
        
        if (processedResult == null)
        {
            _logger.LogError("Failed to process image from Pollinations");
            return null;
        }

        return processedResult;
    }

    /// <summary>
    /// Process and resize image to target dimensions using center crop
    /// </summary>
    private async Task<ImageGenerationOutput?> ProcessAndResizeImageAsync(
        byte[] imageBytes, 
        int targetWidth, 
        int targetHeight,
        CancellationToken ct)
    {
        try
        {
            using var inputStream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync(inputStream, ct);

            // Calculate aspect ratios
            var targetAspect = (double)targetWidth / targetHeight;
            var sourceAspect = (double)image.Width / image.Height;

            // Resize with center crop to maintain aspect ratio
            if (Math.Abs(sourceAspect - targetAspect) > 0.01)
            {
                // Need to crop
                int cropWidth, cropHeight;
                if (sourceAspect > targetAspect)
                {
                    // Source is wider - crop sides
                    cropHeight = image.Height;
                    cropWidth = (int)(cropHeight * targetAspect);
                }
                else
                {
                    // Source is taller - crop top/bottom
                    cropWidth = image.Width;
                    cropHeight = (int)(cropWidth / targetAspect);
                }

                var cropX = (image.Width - cropWidth) / 2;
                var cropY = (image.Height - cropHeight) / 2;

                image.Mutate(ctx => ctx
                    .Crop(new Rectangle(cropX, cropY, cropWidth, cropHeight))
                    .Resize(targetWidth, targetHeight));
            }
            else
            {
                // Just resize
                image.Mutate(ctx => ctx.Resize(targetWidth, targetHeight));
            }

            // Save as JPEG for smaller file size
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 85 }, ct);

            var outputBytes = outputStream.ToArray();

            _logger.LogInformation("Processed image: {Width}x{Height}, {Size} bytes",
                targetWidth, targetHeight, outputBytes.Length);

            return new ImageGenerationOutput(outputBytes, "image/jpeg", targetWidth, targetHeight);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image with ImageSharp");
            return null;
        }
    }
}

/// <summary>
/// Exception for external image generator errors
/// </summary>
public class ExternalImageGeneratorException : Exception
{
    public int StatusCode { get; }

    public ExternalImageGeneratorException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}

