namespace HaberPlatform.Api.Models;

/// <summary>
/// Public article list item (excerpt only)
/// </summary>
public record PublicArticleListItemDto(
    Guid Id,
    string WebTitle,
    string Excerpt,
    DateTime PublishedAtUtc,
    string Path,
    string? CanonicalUrl,
    string? SourceName,
    string? CategoryOrGroup,
    string? PrimaryImageUrl,
    string? PrimaryVideoUrl
);

/// <summary>
/// Full public article
/// </summary>
public record PublicArticleDto(
    Guid Id,
    string WebTitle,
    string WebBody,
    DateTime PublishedAtUtc,
    string Path,
    string Slug,
    string? CanonicalUrl,
    string? SourceName,
    string? CategoryOrGroup,
    string? PrimaryImageUrl,
    string? PrimaryVideoUrl
);

/// <summary>
/// Paginated article list response
/// </summary>
public record PublicArticleListResponse(
    List<PublicArticleListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

/// <summary>
/// Query params for public articles
/// </summary>
public class PublicArticleQueryParams
{
    public string? Q { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

