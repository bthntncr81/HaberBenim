using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HaberPlatform.Api.Services;

/// <summary>
/// Service for managing admin alerts
/// </summary>
public class AlertService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AlertService> _logger;

    public AlertService(AppDbContext db, ILogger<AlertService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Create a new admin alert
    /// </summary>
    public async Task<AdminAlert> CreateAlertAsync(
        string type,
        string severity,
        string title,
        string message,
        object? meta = null)
    {
        var alert = new AdminAlert
        {
            Id = Guid.NewGuid(),
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            MetaJson = meta != null ? JsonSerializer.Serialize(meta) : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AdminAlerts.Add(alert);
        await _db.SaveChangesAsync();

        _logger.LogWarning("Admin alert created: [{Severity}] {Type} - {Title}",
            severity, type, title);

        return alert;
    }

    /// <summary>
    /// Create ingestion down alert
    /// </summary>
    public Task<AdminAlert> CreateIngestionDownAlertAsync(Guid sourceId, string sourceName, string error)
    {
        return CreateAlertAsync(
            AlertTypes.IngestionDown,
            AlertSeverities.Critical,
            $"Ingestion Down: {sourceName}",
            $"Source '{sourceName}' ingestion has failed. Error: {error}",
            new { sourceId, sourceName, error }
        );
    }

    /// <summary>
    /// Create failover activated alert
    /// </summary>
    public Task<AdminAlert> CreateFailoverAlertAsync(string reason)
    {
        return CreateAlertAsync(
            AlertTypes.FailoverActivated,
            AlertSeverities.Critical,
            "Failover Activated",
            reason,
            new { activatedAtUtc = DateTime.UtcNow }
        );
    }

    /// <summary>
    /// Create compliance violation alert
    /// </summary>
    public Task<AdminAlert> CreateComplianceViolationAlertAsync(Guid contentId, string title, string violation)
    {
        return CreateAlertAsync(
            AlertTypes.ComplianceViolation,
            AlertSeverities.Warn,
            $"Compliance Violation: {title}",
            violation,
            new { contentId, title }
        );
    }

    /// <summary>
    /// Create retract alert
    /// </summary>
    public Task<AdminAlert> CreateRetractAlertAsync(Guid contentId, string title, string reason, Guid? userId)
    {
        return CreateAlertAsync(
            AlertTypes.Retract,
            AlertSeverities.Critical,
            $"Content Retracted: {title}",
            $"Content has been retracted. Reason: {reason}",
            new { contentId, title, reason, retractedByUserId = userId }
        );
    }

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    public async Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId)
    {
        var alert = await _db.AdminAlerts.FindAsync(alertId);
        if (alert == null) return false;

        alert.IsAcknowledged = true;
        alert.AcknowledgedAtUtc = DateTime.UtcNow;
        alert.AcknowledgedByUserId = userId;

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Update source ingestion health and create alerts if necessary
    /// </summary>
    public async Task UpdateSourceHealthAsync(Guid sourceId, bool success, string? error = null)
    {
        var source = await _db.Sources
            .Include(s => s.IngestionHealth)
            .FirstOrDefaultAsync(s => s.Id == sourceId);

        if (source == null) return;

        var health = source.IngestionHealth;
        if (health == null)
        {
            health = new SourceIngestionHealth
            {
                Id = Guid.NewGuid(),
                SourceId = sourceId,
                LastSuccessAtUtc = DateTime.UtcNow,
                Status = HealthStatuses.Healthy
            };
            _db.SourceIngestionHealths.Add(health);
        }

        if (success)
        {
            health.LastSuccessAtUtc = DateTime.UtcNow;
            health.ConsecutiveFailures = 0;
            health.Status = HealthStatuses.Healthy;
            health.LastError = null;
        }
        else
        {
            health.LastFailureAtUtc = DateTime.UtcNow;
            health.ConsecutiveFailures++;
            health.LastError = error;

            // Determine status based on failures
            if (health.ConsecutiveFailures >= 5)
            {
                if (health.Status != HealthStatuses.Down)
                {
                    health.Status = HealthStatuses.Down;
                    // Create alert for source going down
                    await CreateIngestionDownAlertAsync(sourceId, source.Name, error ?? "Multiple consecutive failures");
                }
            }
            else if (health.ConsecutiveFailures >= 2)
            {
                health.Status = HealthStatuses.Degraded;
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Check for X connector failover conditions
    /// </summary>
    public async Task CheckFailoverConditionsAsync()
    {
        // Check if any X-type source is Down
        var xSourcesDown = await _db.SourceIngestionHealths
            .Include(h => h.Source)
            .Where(h => h.Source.Type == "X" && h.Status == HealthStatuses.Down)
            .AnyAsync();

        if (xSourcesDown)
        {
            // Check if we already have an unacknowledged failover alert
            var existingAlert = await _db.AdminAlerts
                .Where(a => a.Type == AlertTypes.FailoverActivated && !a.IsAcknowledged)
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (existingAlert == null)
            {
                await CreateFailoverAlertAsync("X data flow down. Failover activated. Some X sources are reporting failures.");

                // Set system setting for failover mode
                var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "FailoverMode");
                if (setting == null)
                {
                    _db.SystemSettings.Add(new SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        Key = "FailoverMode",
                        Value = "true",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    setting.Value = "true";
                }
                await _db.SaveChangesAsync();
            }
        }
    }
}

