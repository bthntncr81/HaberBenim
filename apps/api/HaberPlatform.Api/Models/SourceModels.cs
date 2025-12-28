using System.ComponentModel.DataAnnotations;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Models;

/// <summary>
/// Source list item DTO (for grid/list views)
/// </summary>
public record SourceListItemDto(
    Guid Id,
    string Name,
    string Type,
    string? Identifier,
    string? Url,
    string Category,
    int TrustLevel,
    int Priority,
    bool IsActive,
    string DefaultBehavior,
    DateTime UpdatedAtUtc
);

/// <summary>
/// Source detail DTO (includes more fields)
/// </summary>
public record SourceDetailDto(
    Guid Id,
    string Name,
    string Type,
    string? Identifier,
    string? Url,
    string? Description,
    string Category,
    string? Group,
    int TrustLevel,
    int Priority,
    bool IsActive,
    string DefaultBehavior,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastFetchedAtUtc,
    int FetchIntervalMinutes,
    XSourceStateDto? XState
);

/// <summary>
/// X source state DTO
/// </summary>
public record XSourceStateDto(
    Guid Id,
    string? XUserId,
    string? LastSinceId,
    DateTime? LastPolledAtUtc,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc,
    string? LastError,
    int ConsecutiveFailures
);

/// <summary>
/// Paginated source list response
/// </summary>
public record SourceListResponse(
    List<SourceListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

/// <summary>
/// Request to create or update a source
/// </summary>
public class UpsertSourceRequest : IValidatableObject
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 120 characters")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Type is required")]
    public required string Type { get; set; }

    [StringLength(100, ErrorMessage = "Identifier cannot exceed 100 characters")]
    public string? Identifier { get; set; }

    [StringLength(2048, ErrorMessage = "URL cannot exceed 2048 characters")]
    public string? Url { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Category is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Category must be between 1 and 100 characters")]
    public required string Category { get; set; }

    [Range(0, 100, ErrorMessage = "TrustLevel must be between 0 and 100")]
    public int TrustLevel { get; set; } = 50;

    [Range(0, 1000, ErrorMessage = "Priority must be between 0 and 1000")]
    public int Priority { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "DefaultBehavior is required")]
    public required string DefaultBehavior { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate Type
        if (!SourceTypes.IsValid(Type))
        {
            yield return new ValidationResult(
                $"Type must be one of: {string.Join(", ", SourceTypes.All)}",
                new[] { nameof(Type) }
            );
        }

        // Validate DefaultBehavior
        if (!DefaultBehaviors.IsValid(DefaultBehavior))
        {
            yield return new ValidationResult(
                $"DefaultBehavior must be one of: {string.Join(", ", DefaultBehaviors.All)}",
                new[] { nameof(DefaultBehavior) }
            );
        }

        // Type-specific validation
        if (Type == SourceTypes.RSS)
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                yield return new ValidationResult(
                    "URL is required for RSS sources",
                    new[] { nameof(Url) }
                );
            }
            else if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || 
                     (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                yield return new ValidationResult(
                    "URL must be a valid absolute HTTP or HTTPS URI",
                    new[] { nameof(Url) }
                );
            }
        }

        if (Type == SourceTypes.X)
        {
            if (string.IsNullOrWhiteSpace(Identifier))
            {
                yield return new ValidationResult(
                    "Identifier (username) is required for X sources",
                    new[] { nameof(Identifier) }
                );
            }
            else
            {
                // Clean the identifier (remove @ if present)
                var cleaned = Identifier.TrimStart('@').Trim();
                if (cleaned.Contains(' ') || cleaned.Contains('@'))
                {
                    yield return new ValidationResult(
                        "Identifier must not contain @ or spaces",
                        new[] { nameof(Identifier) }
                    );
                }
            }
        }
    }
}

/// <summary>
/// Request to toggle source active status
/// </summary>
public record ToggleActiveRequest(bool IsActive);

/// <summary>
/// Query parameters for listing sources
/// </summary>
public class SourceQueryParams
{
    public string? Type { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
    public string? Q { get; set; } // Search in name/identifier
    
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    
    [Range(1, 200)]
    public int PageSize { get; set; } = 50;
}

