using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(AppDbContext db, ILogger<SettingsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get settings by prefix (e.g., X_ for X integration settings)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings([FromQuery] string? prefix = null)
    {
        var query = _db.SystemSettings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            query = query.Where(s => s.Key.StartsWith(prefix));
        }

        var settings = await query
            .OrderBy(s => s.Key)
            .Select(s => new SystemSettingDto(s.Key, s.Value))
            .ToListAsync();

        return Ok(settings);
    }

    /// <summary>
    /// Get a single setting by key
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetSetting(string key)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
            return NotFound(new { error = $"Setting '{key}' not found" });

        return Ok(new SystemSettingDto(setting.Key, setting.Value));
    }

    /// <summary>
    /// Update a single setting
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
            return BadRequest(new { error = "Value is required" });

        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
        {
            // Create new setting
            setting = new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = request.Value,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
        }

        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Setting updated: {Key}", key);

        return Ok(new SystemSettingDto(setting.Key, setting.Value));
    }

    /// <summary>
    /// Update multiple settings at once
    /// </summary>
    [HttpPut("batch")]
    public async Task<IActionResult> UpdateSettingsBatch([FromBody] BatchUpdateRequest request)
    {
        if (request.Settings == null || request.Settings.Count == 0)
            return BadRequest(new { error = "Settings array is required" });

        var keys = request.Settings.Select(s => s.Key).ToList();
        var existingSettings = await _db.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key);

        foreach (var dto in request.Settings)
        {
            if (string.IsNullOrWhiteSpace(dto.Key))
                continue;

            if (existingSettings.TryGetValue(dto.Key, out var existing))
            {
                existing.Value = dto.Value ?? "";
            }
            else
            {
                var newSetting = new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    Key = dto.Key,
                    Value = dto.Value ?? "",
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.SystemSettings.Add(newSetting);
            }
        }

        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Batch updated {Count} settings", request.Settings.Count);

        return Ok(new { message = $"Updated {request.Settings.Count} settings" });
    }

    /// <summary>
    /// Delete a setting
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> DeleteSetting(string key)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
            return NotFound(new { error = $"Setting '{key}' not found" });

        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Setting deleted: {Key}", key);

        return NoContent();
    }
}

public record SystemSettingDto(string Key, string Value);
public record UpdateSettingRequest(string Value);
public record BatchUpdateRequest(List<SystemSettingDto> Settings);

