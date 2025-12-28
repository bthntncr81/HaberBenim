namespace HaberPlatform.Api.Services.Publishing;

/// <summary>
/// Configuration options for publishing connectors
/// </summary>
public class PublishingOptions
{
    public const string SectionName = "Publishing";

    public XConnectorOptions X { get; set; } = new();
    public MobilePushConnectorOptions MobilePush { get; set; } = new();
}

public class XConnectorOptions
{
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}

public class MobilePushConnectorOptions
{
    public bool Enabled { get; set; } = false;
    public string ApnsKeyId { get; set; } = string.Empty;
    public string ApnsTeamId { get; set; } = string.Empty;
    public string FcmProjectId { get; set; } = string.Empty;
}

