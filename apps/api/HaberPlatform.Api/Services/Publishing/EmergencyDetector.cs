using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Services.Publishing;

public interface IEmergencyDetector
{
    /// <summary>
    /// Check if content qualifies as emergency
    /// </summary>
    Task<EmergencyDetectionResult> DetectEmergencyAsync(ContentItem content, CancellationToken ct = default);
    
    /// <summary>
    /// Add content to emergency queue
    /// </summary>
    Task<EmergencyQueueItem> EnqueueEmergencyAsync(
        Guid contentItemId,
        EmergencyDetectionResult detection,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get pending emergency items
    /// </summary>
    Task<List<EmergencyQueueItem>> GetPendingEmergenciesAsync(CancellationToken ct = default);
}

public class EmergencyDetectionResult
{
    public bool IsEmergency { get; set; }
    public int Priority { get; set; } = 0;
    public List<string> MatchedKeywords { get; set; } = new();
    public string? Reason { get; set; }
    public bool IsBreakingNews { get; set; }
    public bool CategoryMatch { get; set; }
    public bool TrustedSource { get; set; }

    public static EmergencyDetectionResult NotEmergency() => new() { IsEmergency = false };
    
    public static EmergencyDetectionResult Detected(int priority, List<string> keywords, string reason) => new()
    {
        IsEmergency = true,
        Priority = priority,
        MatchedKeywords = keywords,
        Reason = reason
    };
}

public class EmergencyDetector : IEmergencyDetector
{
    private readonly AppDbContext _db;
    private readonly IPublishingPolicyService _policyService;
    private readonly ILogger<EmergencyDetector> _logger;

    public EmergencyDetector(
        AppDbContext db,
        IPublishingPolicyService policyService,
        ILogger<EmergencyDetector> logger)
    {
        _db = db;
        _policyService = policyService;
        _logger = logger;
    }

    public async Task<EmergencyDetectionResult> DetectEmergencyAsync(ContentItem content, CancellationToken ct = default)
    {
        var policy = await _policyService.GetPolicyAsync(ct);
        var rules = policy.EmergencyRules;
        
        var matchedKeywords = new List<string>();
        int priority = 0;
        var reasons = new List<string>();

        // 1. Check if already marked as breaking news
        if (content.IsBreaking)
        {
            priority += 50;
            reasons.Add("IsBreaking=true");
        }

        // 2. Check category
        var category = content.Source?.Category ?? "";
        if (rules.EmergencyCategories.Any(c => 
            c.Equals(category, StringComparison.OrdinalIgnoreCase)))
        {
            priority += 30;
            reasons.Add($"EmergencyCategory:{category}");
        }

        // 3. Check keywords in title and summary
        var textToCheck = $"{content.Title} {content.Summary}".ToLowerInvariant();
        
        foreach (var keyword in rules.Keywords)
        {
            if (textToCheck.Contains(keyword.ToLowerInvariant()))
            {
                matchedKeywords.Add(keyword);
                priority += 10;
            }
        }

        if (matchedKeywords.Count >= rules.MinKeywordScore)
        {
            reasons.Add($"Keywords:{string.Join(",", matchedKeywords)}");
        }

        // 4. Check trusted source
        if (content.Source != null && rules.TrustedSources.Any(s => 
            s.Equals(content.Source.Name, StringComparison.OrdinalIgnoreCase)))
        {
            priority += 20;
            reasons.Add($"TrustedSource:{content.Source.Name}");
        }

        // Determine if emergency
        bool isEmergency = priority >= 30 || 
                          content.IsBreaking || 
                          matchedKeywords.Count >= rules.MinKeywordScore;

        if (!isEmergency)
        {
            return EmergencyDetectionResult.NotEmergency();
        }

        var result = new EmergencyDetectionResult
        {
            IsEmergency = true,
            Priority = Math.Min(priority, 100),
            MatchedKeywords = matchedKeywords,
            Reason = string.Join("; ", reasons),
            IsBreakingNews = content.IsBreaking,
            CategoryMatch = rules.EmergencyCategories.Any(c => 
                c.Equals(category, StringComparison.OrdinalIgnoreCase)),
            TrustedSource = content.Source != null && rules.TrustedSources.Any(s => 
                s.Equals(content.Source.Name, StringComparison.OrdinalIgnoreCase))
        };

        _logger.LogInformation(
            "Emergency detected for content {ContentId}: Priority={Priority}, Reason={Reason}",
            content.Id, result.Priority, result.Reason);

        return result;
    }

    public async Task<EmergencyQueueItem> EnqueueEmergencyAsync(
        Guid contentItemId,
        EmergencyDetectionResult detection,
        CancellationToken ct = default)
    {
        // Check if already in queue
        var existing = await _db.EmergencyQueueItems
            .FirstOrDefaultAsync(e => e.ContentItemId == contentItemId && 
                                     e.Status == EmergencyQueueStatus.Pending, ct);

        if (existing != null)
        {
            // Update priority if higher
            if (detection.Priority > existing.Priority)
            {
                existing.Priority = detection.Priority;
                existing.MatchedKeywordsCsv = string.Join(",", detection.MatchedKeywords);
                existing.DetectionReason = detection.Reason;
                await _db.SaveChangesAsync(ct);
            }
            return existing;
        }

        var item = new EmergencyQueueItem
        {
            Id = Guid.NewGuid(),
            ContentItemId = contentItemId,
            Priority = detection.Priority,
            Status = EmergencyQueueStatus.Pending,
            MatchedKeywordsCsv = string.Join(",", detection.MatchedKeywords),
            DetectionReason = detection.Reason,
            OverrideSchedule = true,
            DetectedAtUtc = DateTime.UtcNow
        };

        _db.EmergencyQueueItems.Add(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Added content {ContentId} to emergency queue with priority {Priority}",
            contentItemId, detection.Priority);

        return item;
    }

    public async Task<List<EmergencyQueueItem>> GetPendingEmergenciesAsync(CancellationToken ct = default)
    {
        return await _db.EmergencyQueueItems
            .Include(e => e.ContentItem)
            .ThenInclude(c => c!.Source)
            .Where(e => e.Status == EmergencyQueueStatus.Pending)
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.DetectedAtUtc)
            .ToListAsync(ct);
    }
}

