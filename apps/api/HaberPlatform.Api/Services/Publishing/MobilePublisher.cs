using System.Text.Json;
using Microsoft.Extensions.Options;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Stub publisher for mobile push notifications (no real APNs/FCM integration)
/// </summary>
public class MobilePublisher : IChannelPublisher
{
    private readonly ILogger<MobilePublisher> _logger;
    private readonly PublishingOptions _options;

    public string ChannelName => PublishChannels.Mobile;

    public MobilePublisher(ILogger<MobilePublisher> logger, IOptions<PublishingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task<PublishResult> PublishAsync(ContentItem item, ContentDraft draft, CancellationToken ct = default)
    {
        try
        {
            var requestPayload = new
            {
                contentItemId = item.Id,
                pushTitle = draft.PushTitle ?? item.Title,
                pushBody = draft.PushBody ?? item.Summary ?? item.BodyText[..Math.Min(200, item.BodyText.Length)],
                mobileSummary = draft.MobileSummary
            };

            var requestJson = JsonSerializer.Serialize(requestPayload);

            // Check if real connector is enabled
            var isStub = !_options.MobilePush.Enabled;

            // Stub: no real push notification sent
            var responsePayload = new
            {
                status = isStub ? "stub" : "simulated",
                stub = isStub,
                connectorEnabled = _options.MobilePush.Enabled,
                message = isStub 
                    ? "Mobile push connector is disabled (stub mode)" 
                    : "Push notification would be sent to mobile devices",
                pushTitle = requestPayload.pushTitle,
                pushBody = requestPayload.pushBody,
                timestamp = DateTime.UtcNow
            };

            if (isStub)
            {
                _logger.LogInformation("Mobile push for content {ContentId} - stub mode (connector disabled)", item.Id);
            }
            else
            {
                _logger.LogInformation("Simulated mobile push for content {ContentId}", item.Id);
            }

            return Task.FromResult(PublishResult.Succeeded(requestJson, JsonSerializer.Serialize(responsePayload)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process mobile push for content {ContentId}", item.Id);
            return Task.FromResult(PublishResult.Failed(ex.Message));
        }
    }
}
