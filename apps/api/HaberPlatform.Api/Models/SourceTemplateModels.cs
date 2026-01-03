namespace HaberPlatform.Api.Models;

// ========== SOURCE TEMPLATE ASSIGNMENT MODELS ==========

public class SourceTemplateAssignmentDto
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Mode { get; set; } = "Auto";
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = "";
    public string TemplateFormat { get; set; } = "";
    public int? PriorityOverride { get; set; }
    public int EffectivePriority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class CreateSourceTemplateAssignmentRequest
{
    public Guid SourceId { get; set; }
    public required string Platform { get; set; }
    public Guid TemplateId { get; set; }
    public int? PriorityOverride { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSourceTemplateAssignmentRequest
{
    public int? PriorityOverride { get; set; }
    public bool? IsActive { get; set; }
}

public class SourceTemplateAssignmentListQuery
{
    public Guid? SourceId { get; set; }
    public string? Platform { get; set; }
    public Guid? TemplateId { get; set; }
    public bool? Active { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// ========== APPLY TEMPLATE MODELS ==========

public class ApplyTemplateRequest
{
    public required string Platform { get; set; }
}

public class ApplyTemplateResponse
{
    public bool Success { get; set; }
    public Guid? SelectedTemplateId { get; set; }
    public string? SelectedTemplateName { get; set; }
    public string? Format { get; set; }
    public string? MediaType { get; set; }
    public string? SkipReason { get; set; }
    public string? Error { get; set; }
    
    // Resolved text spec
    public ResolvedTextSpecDto? ResolvedTextSpec { get; set; }
    
    // Optional preview URL
    public string? PreviewVisualUrl { get; set; }
}

public class ResolvedTextSpecDto
{
    public string? InstagramCaption { get; set; }
    public string? XText { get; set; }
    public string? TiktokHook { get; set; }
    public string? YoutubeTitle { get; set; }
    public string? YoutubeDescription { get; set; }
}

// ========== BULK ASSIGNMENT MODELS ==========

public class BulkAssignTemplateRequest
{
    public List<Guid> SourceIds { get; set; } = new();
    public required string Platform { get; set; }
    public Guid TemplateId { get; set; }
    public int? PriorityOverride { get; set; }
}

public class BulkAssignTemplateResponse
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

