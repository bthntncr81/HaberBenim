using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using System.Security.Claims;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/rules")]
[Authorize(Roles = "Admin")]
public class RulesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RuleEngineService _ruleEngine;
    private readonly ILogger<RulesController> _logger;

    public RulesController(AppDbContext db, RuleEngineService ruleEngine, ILogger<RulesController> logger)
    {
        _db = db;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    /// <summary>
    /// Get all rules
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RuleDto>>> GetRules()
    {
        var rules = await _db.Rules
            .Include(r => r.CreatedByUser)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return rules.Select(r => r.ToDto()).ToList();
    }

    /// <summary>
    /// Get a single rule by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RuleDto>> GetRule(Guid id)
    {
        var rule = await _db.Rules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rule == null)
            return NotFound(new { error = "Rule not found" });

        return rule.ToDto();
    }

    /// <summary>
    /// Create a new rule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RuleDto>> CreateRule([FromBody] CreateRuleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate decision type
        if (!DecisionTypes.All.Contains(request.DecisionType))
            return BadRequest(new { error = $"Invalid DecisionType. Must be one of: {string.Join(", ", DecisionTypes.All)}" });

        // Check for duplicate name
        var existingName = await _db.Rules.AnyAsync(r => r.Name == request.Name);
        if (existingName)
            return Conflict(new { error = "A rule with this name already exists" });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? createdByUserId = Guid.TryParse(userId, out var uid) ? uid : null;

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            DecisionType = request.DecisionType,
            MinTrustLevel = request.MinTrustLevel,
            KeywordsIncludeCsv = request.KeywordsIncludeCsv?.Trim(),
            KeywordsExcludeCsv = request.KeywordsExcludeCsv?.Trim(),
            SourceIdsCsv = request.SourceIdsCsv?.Trim(),
            GroupIdsCsv = request.GroupIdsCsv?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };

        _db.Rules.Add(rule);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Rule created: {RuleName} (ID: {RuleId}) by user {UserId}", 
            rule.Name, rule.Id, createdByUserId);

        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule.ToDto());
    }

    /// <summary>
    /// Update an existing rule
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RuleDto>> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate decision type
        if (!DecisionTypes.All.Contains(request.DecisionType))
            return BadRequest(new { error = $"Invalid DecisionType. Must be one of: {string.Join(", ", DecisionTypes.All)}" });

        var rule = await _db.Rules.FindAsync(id);
        if (rule == null)
            return NotFound(new { error = "Rule not found" });

        // Check for duplicate name (excluding this rule)
        var existingName = await _db.Rules.AnyAsync(r => r.Name == request.Name && r.Id != id);
        if (existingName)
            return Conflict(new { error = "A rule with this name already exists" });

        rule.Name = request.Name;
        rule.IsEnabled = request.IsEnabled;
        rule.Priority = request.Priority;
        rule.DecisionType = request.DecisionType;
        rule.MinTrustLevel = request.MinTrustLevel;
        rule.KeywordsIncludeCsv = request.KeywordsIncludeCsv?.Trim();
        rule.KeywordsExcludeCsv = request.KeywordsExcludeCsv?.Trim();
        rule.SourceIdsCsv = request.SourceIdsCsv?.Trim();
        rule.GroupIdsCsv = request.GroupIdsCsv?.Trim();

        await _db.SaveChangesAsync();

        _logger.LogInformation("Rule updated: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);

        return rule.ToDto();
    }

    /// <summary>
    /// Delete a rule
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteRule(Guid id)
    {
        var rule = await _db.Rules.FindAsync(id);
        if (rule == null)
            return NotFound(new { error = "Rule not found" });

        _db.Rules.Remove(rule);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Rule deleted: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);

        return NoContent();
    }

    /// <summary>
    /// Evaluate a single content item against the rules
    /// </summary>
    [HttpPost("evaluate/{contentId:guid}")]
    public async Task<ActionResult<RuleDecisionResult>> EvaluateContent(Guid contentId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null)
            return NotFound(new { error = "Content item not found" });

        var decision = await _ruleEngine.EvaluateAsync(item, item.Source);
        _ruleEngine.ApplyDecision(item, item.Source, decision);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Content {ContentId} evaluated: {DecisionType} - {Reason}", 
            contentId, decision.DecisionType, decision.Reason);

        return decision;
    }

    /// <summary>
    /// Recompute decisions for multiple content items based on filters
    /// </summary>
    [HttpPost("recompute")]
    public async Task<ActionResult<RecomputeResult>> Recompute([FromBody] RecomputeRequest request)
    {
        const int maxItems = 5000;

        var query = _db.ContentItems
            .Include(c => c.Source)
            .AsQueryable();

        if (request.FromUtc.HasValue)
            query = query.Where(c => c.PublishedAtUtc >= request.FromUtc.Value);

        if (request.ToUtc.HasValue)
            query = query.Where(c => c.PublishedAtUtc <= request.ToUtc.Value);

        if (request.SourceId.HasValue)
            query = query.Where(c => c.SourceId == request.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(c => c.Status == request.Status);

        // Exclude duplicates from recompute
        query = query.Where(c => c.Status != ContentStatuses.Duplicate);

        var items = await query
            .OrderByDescending(c => c.PublishedAtUtc)
            .Take(maxItems)
            .ToListAsync();

        var result = new RecomputeResult
        {
            Processed = items.Count
        };

        foreach (var item in items)
        {
            var oldDecisionType = item.DecisionType;
            var oldStatus = item.Status;

            var decision = await _ruleEngine.EvaluateAsync(item, item.Source);
            _ruleEngine.ApplyDecision(item, item.Source, decision);

            // Track if changed
            if (oldDecisionType != item.DecisionType || oldStatus != item.Status)
            {
                result.Changed++;
            }

            // Track by decision type
            if (!result.ByDecisionTypeCounts.ContainsKey(decision.DecisionType))
                result.ByDecisionTypeCounts[decision.DecisionType] = 0;
            result.ByDecisionTypeCounts[decision.DecisionType]++;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Recompute completed: {Processed} processed, {Changed} changed", 
            result.Processed, result.Changed);

        return result;
    }
}

