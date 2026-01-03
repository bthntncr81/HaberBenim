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
[Route("api/v1/templates")]
[Authorize(Roles = "Admin")]
public class TemplatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITemplateVariableResolver _variableResolver;
    private readonly ITemplatePreviewRenderer _previewRenderer;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        AppDbContext db,
        ITemplateVariableResolver variableResolver,
        ITemplatePreviewRenderer previewRenderer,
        ILogger<TemplatesController> logger)
    {
        _db = db;
        _variableResolver = variableResolver;
        _previewRenderer = previewRenderer;
        _logger = logger;
    }

    /// <summary>
    /// List templates with optional filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TemplateListQuery query, CancellationToken ct)
    {
        var q = _db.PublishTemplates
            .Include(t => t.Spec)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Platform))
            q = q.Where(t => t.Platform == query.Platform);

        if (!string.IsNullOrEmpty(query.Format))
            q = q.Where(t => t.Format == query.Format);

        if (query.Active.HasValue)
            q = q.Where(t => t.IsActive == query.Active.Value);

        if (!string.IsNullOrEmpty(query.Q))
            q = q.Where(t => t.Name.ToLower().Contains(query.Q.ToLower()));

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(t => t.Platform)
            .ThenBy(t => t.Priority)
            .ThenBy(t => t.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Platform = t.Platform,
                Format = t.Format,
                Priority = t.Priority,
                IsActive = t.IsActive,
                RuleJson = t.RuleJson,
                HasSpec = t.Spec != null,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new
        {
            items,
            total,
            page = query.Page,
            pageSize = query.PageSize,
            totalPages = (int)Math.Ceiling(total / (double)query.PageSize)
        });
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var template = await _db.PublishTemplates
            .Include(t => t.Spec)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template == null)
            return NotFound(new { error = "Template not found" });

        return Ok(new TemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Platform = template.Platform,
            Format = template.Format,
            Priority = template.Priority,
            IsActive = template.IsActive,
            RuleJson = template.RuleJson,
            HasSpec = template.Spec != null,
            CreatedAtUtc = template.CreatedAtUtc,
            UpdatedAtUtc = template.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Create new template
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        // Validate platform
        if (!TemplatePlatforms.All.Contains(request.Platform))
        {
            return BadRequest(new { error = $"Invalid platform. Allowed: {string.Join(", ", TemplatePlatforms.All)}" });
        }

        // Validate format
        if (!TemplateFormats.All.Contains(request.Format))
        {
            return BadRequest(new { error = $"Invalid format. Allowed: {string.Join(", ", TemplateFormats.All)}" });
        }

        // Check for duplicate name
        var exists = await _db.PublishTemplates.AnyAsync(t => t.Name == request.Name, ct);
        if (exists)
        {
            return BadRequest(new { error = "Template with this name already exists" });
        }

        var template = new PublishTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Platform = request.Platform,
            Format = request.Format,
            Priority = request.Priority,
            IsActive = request.IsActive,
            RuleJson = request.RuleJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.PublishTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created template {TemplateId}: {Name} ({Platform}/{Format})",
            template.Id, template.Name, template.Platform, template.Format);

        return CreatedAtAction(nameof(Get), new { id = template.Id }, new TemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Platform = template.Platform,
            Format = template.Format,
            Priority = template.Priority,
            IsActive = template.IsActive,
            RuleJson = template.RuleJson,
            HasSpec = false,
            CreatedAtUtc = template.CreatedAtUtc,
            UpdatedAtUtc = template.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Update template metadata
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken ct)
    {
        var template = await _db.PublishTemplates.FindAsync([id], ct);
        if (template == null)
            return NotFound(new { error = "Template not found" });

        if (request.Name != null)
        {
            var exists = await _db.PublishTemplates.AnyAsync(t => t.Name == request.Name && t.Id != id, ct);
            if (exists)
                return BadRequest(new { error = "Template with this name already exists" });
            template.Name = request.Name;
        }

        if (request.Platform != null)
        {
            if (!TemplatePlatforms.All.Contains(request.Platform))
                return BadRequest(new { error = $"Invalid platform. Allowed: {string.Join(", ", TemplatePlatforms.All)}" });
            template.Platform = request.Platform;
        }

        if (request.Format != null)
        {
            if (!TemplateFormats.All.Contains(request.Format))
                return BadRequest(new { error = $"Invalid format. Allowed: {string.Join(", ", TemplateFormats.All)}" });
            template.Format = request.Format;
        }

        if (request.Priority.HasValue)
            template.Priority = request.Priority.Value;

        if (request.IsActive.HasValue)
            template.IsActive = request.IsActive.Value;

        if (request.RuleJson != null)
            template.RuleJson = request.RuleJson;

        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated template {TemplateId}", id);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete template
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var template = await _db.PublishTemplates.FindAsync([id], ct);
        if (template == null)
            return NotFound(new { error = "Template not found" });

        _db.PublishTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted template {TemplateId}", id);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Get template spec (visual + text)
    /// </summary>
    [HttpGet("{id:guid}/spec")]
    public async Task<IActionResult> GetSpec(Guid id, CancellationToken ct)
    {
        var template = await _db.PublishTemplates
            .Include(t => t.Spec)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template == null)
            return NotFound(new { error = "Template not found" });

        if (template.Spec == null)
        {
            return Ok(new TemplateSpecDto
            {
                Id = Guid.Empty,
                TemplateId = id,
                VisualSpecJson = null,
                TextSpecJson = null,
                CreatedAtUtc = DateTime.MinValue,
                UpdatedAtUtc = DateTime.MinValue
            });
        }

        return Ok(new TemplateSpecDto
        {
            Id = template.Spec.Id,
            TemplateId = template.Spec.TemplateId,
            VisualSpecJson = template.Spec.VisualSpecJson,
            TextSpecJson = template.Spec.TextSpecJson,
            CreatedAtUtc = template.Spec.CreatedAtUtc,
            UpdatedAtUtc = template.Spec.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Save template spec (visual + text)
    /// </summary>
    [HttpPut("{id:guid}/spec")]
    public async Task<IActionResult> SaveSpec(Guid id, [FromBody] UpdateTemplateSpecRequest request, CancellationToken ct)
    {
        var template = await _db.PublishTemplates
            .Include(t => t.Spec)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template == null)
            return NotFound(new { error = "Template not found" });

        // Validate visual spec JSON if provided
        if (!string.IsNullOrEmpty(request.VisualSpecJson))
        {
            try
            {
                var visualSpec = JsonSerializer.Deserialize<VisualSpec>(request.VisualSpecJson);
                if (visualSpec == null)
                    return BadRequest(new { error = "Invalid visual spec JSON" });
            }
            catch (JsonException ex)
            {
                return BadRequest(new { error = $"Invalid visual spec JSON: {ex.Message}" });
            }
        }

        // Validate text spec JSON if provided
        if (!string.IsNullOrEmpty(request.TextSpecJson))
        {
            try
            {
                var textSpec = JsonSerializer.Deserialize<TextSpec>(request.TextSpecJson);
                if (textSpec == null)
                    return BadRequest(new { error = "Invalid text spec JSON" });
            }
            catch (JsonException ex)
            {
                return BadRequest(new { error = $"Invalid text spec JSON: {ex.Message}" });
            }
        }

        if (template.Spec == null)
        {
            template.Spec = new PublishTemplateSpec
            {
                Id = Guid.NewGuid(),
                TemplateId = id,
                VisualSpecJson = request.VisualSpecJson,
                TextSpecJson = request.TextSpecJson,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.PublishTemplateSpecs.Add(template.Spec);
        }
        else
        {
            if (request.VisualSpecJson != null)
                template.Spec.VisualSpecJson = request.VisualSpecJson;
            if (request.TextSpecJson != null)
                template.Spec.TextSpecJson = request.TextSpecJson;
            template.Spec.UpdatedAtUtc = DateTime.UtcNow;
        }

        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Saved spec for template {TemplateId}", id);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Generate preview image for template with content
    /// </summary>
    [HttpPost("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, [FromBody] TemplatePreviewRequest request, CancellationToken ct)
    {
        var template = await _db.PublishTemplates
            .Include(t => t.Spec)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template == null)
            return NotFound(new { error = "Template not found" });

        if (template.Spec == null || string.IsNullOrEmpty(template.Spec.VisualSpecJson))
            return BadRequest(new { error = "Template has no visual spec" });

        // Load content - try ContentItem first, then PublishedContent
        var content = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Include(c => c.PublishedContent)
            .Include(c => c.Media)
            .Include(c => c.MediaLinks)
                .ThenInclude(ml => ml.MediaAsset)
            .FirstOrDefaultAsync(c => c.Id == request.ContentItemId, ct);

        // If not found, try looking up via PublishedContent
        if (content == null)
        {
            var published = await _db.PublishedContents
                .Include(p => p.ContentItem)
                    .ThenInclude(c => c.Source)
                .Include(p => p.ContentItem)
                    .ThenInclude(c => c.Draft)
                .Include(p => p.ContentItem)
                    .ThenInclude(c => c.Media)
                .Include(p => p.ContentItem)
                    .ThenInclude(c => c.MediaLinks)
                        .ThenInclude(ml => ml.MediaAsset)
                .FirstOrDefaultAsync(p => p.Id == request.ContentItemId, ct);
            
            if (published != null)
            {
                content = published.ContentItem;
            }
        }

        if (content == null)
            return NotFound(new { error = "Content item not found" });

        // Render preview
        var result = await _previewRenderer.RenderPreviewAsync(
            template,
            content,
            content.PublishedContent,
            ct);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        // Resolve text spec if available
        TextSpec? resolvedTextSpec = null;
        if (!string.IsNullOrEmpty(template.Spec.TextSpecJson))
        {
            try
            {
                var textSpec = JsonSerializer.Deserialize<TextSpec>(template.Spec.TextSpecJson);
                if (textSpec != null)
                {
                    resolvedTextSpec = new TextSpec
                    {
                        InstagramCaption = textSpec.InstagramCaption != null
                            ? _variableResolver.ResolveText(textSpec.InstagramCaption, result.ResolvedVars)
                            : null,
                        XText = textSpec.XText != null
                            ? _variableResolver.ResolveText(textSpec.XText, result.ResolvedVars)
                            : null,
                        TiktokHook = textSpec.TiktokHook != null
                            ? _variableResolver.ResolveText(textSpec.TiktokHook, result.ResolvedVars)
                            : null,
                        YoutubeTitle = textSpec.YoutubeTitle != null
                            ? _variableResolver.ResolveText(textSpec.YoutubeTitle, result.ResolvedVars)
                            : null,
                        YoutubeDescription = textSpec.YoutubeDescription != null
                            ? _variableResolver.ResolveText(textSpec.YoutubeDescription, result.ResolvedVars)
                            : null
                    };
                }
            }
            catch (JsonException)
            {
                // Ignore text spec errors during preview
            }
        }

        return Ok(new TemplatePreviewResponse
        {
            PreviewUrl = result.PreviewUrl ?? "",
            ResolvedVars = result.ResolvedVars,
            ResolvedTextSpec = resolvedTextSpec
        });
    }

    /// <summary>
    /// Get available platforms and formats
    /// </summary>
    [HttpGet("options")]
    [AllowAnonymous]
    public IActionResult GetOptions()
    {
        return Ok(new
        {
            platforms = TemplatePlatforms.All,
            formats = TemplateFormats.All
        });
    }

    // ========== TEMPLATE ASSETS ==========

    /// <summary>
    /// List template assets
    /// </summary>
    [HttpGet("assets")]
    public async Task<IActionResult> ListAssets(CancellationToken ct)
    {
        var assets = await _db.TemplateAssets
            .OrderBy(a => a.Key)
            .Select(a => new TemplateAssetDto
            {
                Id = a.Id,
                Key = a.Key,
                ContentType = a.ContentType,
                StoragePath = a.StoragePath,
                Width = a.Width,
                Height = a.Height,
                Url = $"/media/{a.StoragePath}",
                CreatedAtUtc = a.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new { items = assets });
    }

    /// <summary>
    /// Upload template asset
    /// </summary>
    [HttpPost("assets")]
    public async Task<IActionResult> UploadAsset(
        [FromForm] string key,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        // Validate key
        if (string.IsNullOrWhiteSpace(key) || !System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-z0-9_]+$"))
            return BadRequest(new { error = "Invalid key. Use lowercase letters, numbers, and underscores only." });

        // Check for existing key
        var exists = await _db.TemplateAssets.AnyAsync(a => a.Key == key, ct);
        if (exists)
            return BadRequest(new { error = "Asset with this key already exists" });

        // Validate content type
        var allowedTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/gif", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"Invalid file type. Allowed: {string.Join(", ", allowedTypes)}" });

        // Read and validate image
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        int width, height;
        try
        {
            ms.Position = 0;
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(ms, ct);
            width = image.Width;
            height = image.Height;
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Invalid image: {ex.Message}" });
        }

        // Determine extension
        var extension = file.ContentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            _ => ".bin"
        };

        // Save file
        var storagePath = $"assets/{key}{extension}";
        var mediaRootDir = Path.Combine(Directory.GetCurrentDirectory(), "tools/storage/media");
        var assetsDir = Path.Combine(mediaRootDir, "assets");
        Directory.CreateDirectory(assetsDir);

        var fullPath = Path.Combine(mediaRootDir, storagePath);
        await System.IO.File.WriteAllBytesAsync(fullPath, bytes, ct);

        // Create entity
        var asset = new TemplateAsset
        {
            Id = Guid.NewGuid(),
            Key = key,
            ContentType = file.ContentType,
            StoragePath = storagePath,
            Width = width,
            Height = height,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TemplateAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Uploaded template asset {Key} ({Width}x{Height})", key, width, height);

        return Ok(new TemplateAssetDto
        {
            Id = asset.Id,
            Key = asset.Key,
            ContentType = asset.ContentType,
            StoragePath = asset.StoragePath,
            Width = asset.Width,
            Height = asset.Height,
            Url = $"/media/{asset.StoragePath}",
            CreatedAtUtc = asset.CreatedAtUtc
        });
    }

    /// <summary>
    /// Delete template asset
    /// </summary>
    [HttpDelete("assets/{key}")]
    public async Task<IActionResult> DeleteAsset(string key, CancellationToken ct)
    {
        var asset = await _db.TemplateAssets.FirstOrDefaultAsync(a => a.Key == key, ct);
        if (asset == null)
            return NotFound(new { error = "Asset not found" });

        // Delete file
        var mediaRootDir = Path.Combine(Directory.GetCurrentDirectory(), "tools/storage/media");
        var fullPath = Path.Combine(mediaRootDir, asset.StoragePath);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        _db.TemplateAssets.Remove(asset);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted template asset {Key}", key);

        return Ok(new { success = true });
    }
}

