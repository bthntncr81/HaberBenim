using System.Text.Json;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/editorial/items/{id:guid}/template")]
[Authorize(Roles = "Admin,Editor")]
public class EditorialTemplateController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITemplateSelector _templateSelector;
    private readonly ITemplateVariableResolver _variableResolver;
    private readonly ITemplatePreviewRenderer _previewRenderer;
    private readonly ILogger<EditorialTemplateController> _logger;

    public EditorialTemplateController(
        AppDbContext db,
        ITemplateSelector templateSelector,
        ITemplateVariableResolver variableResolver,
        ITemplatePreviewRenderer previewRenderer,
        ILogger<EditorialTemplateController> logger)
    {
        _db = db;
        _templateSelector = templateSelector;
        _variableResolver = variableResolver;
        _previewRenderer = previewRenderer;
        _logger = logger;
    }

    /// <summary>
    /// Apply template to content item for a specific platform
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyTemplate(
        Guid id,
        [FromBody] ApplyTemplateRequest request,
        CancellationToken ct)
    {
        // Validate platform
        if (!TemplatePlatforms.All.Contains(request.Platform))
        {
            return BadRequest(new ApplyTemplateResponse
            {
                Success = false,
                Error = $"Invalid platform. Allowed: {string.Join(", ", TemplatePlatforms.All)}"
            });
        }

        // Load content with all needed relations
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Include(c => c.PublishedContent)
            .Include(c => c.MediaLinks)
                .ThenInclude(m => m.MediaAsset)
            .Include(c => c.Media)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (content == null)
        {
            return NotFound(new ApplyTemplateResponse
            {
                Success = false,
                Error = "Content item not found"
            });
        }

        // Select template
        var selectionResult = await _templateSelector.SelectTemplateAsync(content, request.Platform, ct);

        if (!selectionResult.Success)
        {
            return Ok(new ApplyTemplateResponse
            {
                Success = false,
                MediaType = selectionResult.MediaType.ToString(),
                SkipReason = selectionResult.SkipReason,
                Error = selectionResult.Error
            });
        }

        var template = selectionResult.Template!;

        // Resolve variables
        var vars = _variableResolver.ResolveVariables(content, content.PublishedContent);

        // Resolve text spec
        ResolvedTextSpecDto? resolvedTextSpec = null;
        if (template.Spec?.TextSpecJson != null)
        {
            try
            {
                var textSpec = JsonSerializer.Deserialize<TextSpec>(template.Spec.TextSpecJson);
                if (textSpec != null)
                {
                    resolvedTextSpec = new ResolvedTextSpecDto
                    {
                        InstagramCaption = textSpec.InstagramCaption != null
                            ? _variableResolver.ResolveText(textSpec.InstagramCaption, vars)
                            : null,
                        XText = textSpec.XText != null
                            ? _variableResolver.ResolveText(textSpec.XText, vars)
                            : null,
                        TiktokHook = textSpec.TiktokHook != null
                            ? _variableResolver.ResolveText(textSpec.TiktokHook, vars)
                            : null,
                        YoutubeTitle = textSpec.YoutubeTitle != null
                            ? _variableResolver.ResolveText(textSpec.YoutubeTitle, vars)
                            : null,
                        YoutubeDescription = textSpec.YoutubeDescription != null
                            ? _variableResolver.ResolveText(textSpec.YoutubeDescription, vars)
                            : null
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse text spec for template {TemplateId}", template.Id);
            }
        }

        // Optionally render preview (if visual spec exists)
        string? previewUrl = null;
        if (template.Spec?.VisualSpecJson != null)
        {
            var previewResult = await _previewRenderer.RenderPreviewAsync(
                template,
                content,
                content.PublishedContent,
                ct);

            if (previewResult.Success)
            {
                previewUrl = previewResult.PreviewUrl;
            }
        }

        _logger.LogInformation("Applied template {TemplateId} ({TemplateName}) to content {ContentId} for platform {Platform}",
            template.Id, template.Name, id, request.Platform);

        return Ok(new ApplyTemplateResponse
        {
            Success = true,
            SelectedTemplateId = template.Id,
            SelectedTemplateName = template.Name,
            Format = selectionResult.Format,
            MediaType = selectionResult.MediaType.ToString(),
            ResolvedTextSpec = resolvedTextSpec,
            PreviewVisualUrl = previewUrl
        });
    }

    /// <summary>
    /// Preview template selection for all platforms without applying
    /// </summary>
    [HttpGet("preview-selection")]
    public async Task<IActionResult> PreviewSelection(Guid id, CancellationToken ct)
    {
        // Load content
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.MediaLinks)
                .ThenInclude(m => m.MediaAsset)
            .Include(c => c.Media)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (content == null)
        {
            return NotFound(new { error = "Content item not found" });
        }

        var results = new Dictionary<string, object>();

        foreach (var platform in TemplatePlatforms.All)
        {
            var result = await _templateSelector.SelectTemplateAsync(content, platform, ct);
            
            results[platform] = new
            {
                success = result.Success,
                templateId = result.Template?.Id,
                templateName = result.Template?.Name,
                format = result.Format,
                mediaType = result.MediaType.ToString(),
                skipReason = result.SkipReason,
                error = result.Error
            };
        }

        return Ok(results);
    }
}

