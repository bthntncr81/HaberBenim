using System.ComponentModel.DataAnnotations;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Models;

// DTO for Rule list/detail
public class RuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public string DecisionType { get; set; } = string.Empty;
    public int? MinTrustLevel { get; set; }
    public string? KeywordsIncludeCsv { get; set; }
    public string? KeywordsExcludeCsv { get; set; }
    public string? SourceIdsCsv { get; set; }
    public string? GroupIdsCsv { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByUserEmail { get; set; }
}

// Create request
public class CreateRuleRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    
    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 0;
    
    [Required]
    public string DecisionType { get; set; } = DecisionTypes.RequireApproval;
    
    public int? MinTrustLevel { get; set; }
    public string? KeywordsIncludeCsv { get; set; }
    public string? KeywordsExcludeCsv { get; set; }
    public string? SourceIdsCsv { get; set; }
    public string? GroupIdsCsv { get; set; }
}

// Update request
public class UpdateRuleRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    
    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 0;
    
    [Required]
    public string DecisionType { get; set; } = DecisionTypes.RequireApproval;
    
    public int? MinTrustLevel { get; set; }
    public string? KeywordsIncludeCsv { get; set; }
    public string? KeywordsExcludeCsv { get; set; }
    public string? SourceIdsCsv { get; set; }
    public string? GroupIdsCsv { get; set; }
}

// Rule decision result
public class RuleDecisionResult
{
    public string DecisionType { get; set; } = DecisionTypes.RequireApproval;
    public string Status { get; set; } = ContentStatuses.PendingApproval;
    public Guid? RuleId { get; set; }
    public string? RuleName { get; set; }
    public string Reason { get; set; } = "Default";
    public DateTime? ScheduledAtUtc { get; set; }
}

// Recompute request
public class RecomputeRequest
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public Guid? SourceId { get; set; }
    public string? Status { get; set; }
}

// Recompute result
public class RecomputeResult
{
    public int Processed { get; set; }
    public int Changed { get; set; }
    public Dictionary<string, int> ByDecisionTypeCounts { get; set; } = new();
}

// Extension methods for mapping
public static class RuleExtensions
{
    public static RuleDto ToDto(this Rule rule)
    {
        return new RuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            IsEnabled = rule.IsEnabled,
            Priority = rule.Priority,
            DecisionType = rule.DecisionType,
            MinTrustLevel = rule.MinTrustLevel,
            KeywordsIncludeCsv = rule.KeywordsIncludeCsv,
            KeywordsExcludeCsv = rule.KeywordsExcludeCsv,
            SourceIdsCsv = rule.SourceIdsCsv,
            GroupIdsCsv = rule.GroupIdsCsv,
            CreatedAtUtc = rule.CreatedAtUtc,
            CreatedByUserId = rule.CreatedByUserId,
            CreatedByUserEmail = rule.CreatedByUser?.Email
        };
    }
}

