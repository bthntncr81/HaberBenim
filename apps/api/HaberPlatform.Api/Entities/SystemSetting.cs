namespace HaberPlatform.Api.Entities;

public class SystemSetting
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

