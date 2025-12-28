using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;

namespace HaberPlatform.Api.Services;

public class EditorialService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EditorialService> _logger;

    public EditorialService(AppDbContext db, ILogger<EditorialService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get inbox items based on filters
    /// </summary>
    public async Task<EditorialInboxResponse> GetInboxAsync(EditorialInboxQuery query)
    {
        var q = _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Where(c => c.Status != ContentStatuses.Duplicate)
            .AsQueryable();

        // Filter by status (default: PendingApproval)
        var status = string.IsNullOrWhiteSpace(query.Status) 
            ? ContentStatuses.PendingApproval 
            : query.Status;
        
        q = q.Where(c => c.Status == status);

        if (query.FromUtc.HasValue)
            q = q.Where(c => c.PublishedAtUtc >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            q = q.Where(c => c.PublishedAtUtc <= query.ToUtc.Value);

        if (query.SourceId.HasValue)
            q = q.Where(c => c.SourceId == query.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            q = q.Where(c =>
                EF.Functions.ILike(c.Title, $"%{query.Keyword}%") ||
                (c.Summary != null && EF.Functions.ILike(c.Summary, $"%{query.Keyword}%")) ||
                EF.Functions.ILike(c.BodyText, $"%{query.Keyword}%"));
        }

        var total = await q.CountAsync();

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(c => c.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new EditorialInboxItemDto(
                c.Id,
                c.PublishedAtUtc,
                c.Source.Name,
                c.Title,
                c.Summary,
                c.Status,
                c.DecisionType,
                c.DecidedAtUtc,
                c.ScheduledAtUtc,
                c.Draft != null
            ))
            .ToListAsync();

        return new EditorialInboxResponse(items, total, page, pageSize);
    }

    /// <summary>
    /// Get full item details including draft and revisions
    /// </summary>
    public async Task<EditorialItemDto?> GetItemAsync(Guid id)
    {
        var item = await _db.ContentItems
            .Include(c => c.Source)
            .Include(c => c.Draft)
            .Include(c => c.Media)
            .Include(c => c.Revisions.OrderByDescending(r => r.VersionNo).Take(10))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null) return null;

        return new EditorialItemDto(
            item.Id,
            item.PublishedAtUtc,
            item.IngestedAtUtc,
            item.SourceId,
            item.Source.Name,
            item.Title,
            item.Summary,
            item.BodyText,
            item.OriginalText,
            item.CanonicalUrl,
            item.Language,
            item.Status,
            item.DecisionType,
            item.DecisionReason,
            item.DecidedAtUtc,
            item.ScheduledAtUtc,
            item.TrustLevelSnapshot,
            item.EditorialNote,
            item.RejectionReason,
            item.LastEditedByUserId,
            item.LastEditedAtUtc,
            item.Draft != null ? new EditorialDraftDto(
                item.Draft.Id,
                item.Draft.XText,
                item.Draft.WebTitle,
                item.Draft.WebBody,
                item.Draft.MobileSummary,
                item.Draft.PushTitle,
                item.Draft.PushBody,
                item.Draft.HashtagsCsv,
                item.Draft.MentionsCsv,
                item.Draft.PublishToWeb,
                item.Draft.PublishToMobile,
                item.Draft.PublishToX,
                item.Draft.UpdatedAtUtc,
                item.Draft.UpdatedByUserId
            ) : null,
            item.Media.Select(m => new EditorialMediaDto(
                m.Id,
                m.MediaType,
                m.Url,
                m.ThumbUrl
            )).ToList(),
            item.Revisions.Select(r => new EditorialRevisionDto(
                r.Id,
                r.VersionNo,
                r.ActionType,
                r.CreatedAtUtc,
                r.CreatedByUserId
            )).ToList()
        );
    }

    /// <summary>
    /// Save or update draft for a content item
    /// </summary>
    public async Task<SaveDraftResponse?> SaveDraftAsync(Guid contentId, SaveDraftRequest request, Guid userId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null) return null;

        // Upsert draft
        if (item.Draft == null)
        {
            item.Draft = new ContentDraft
            {
                Id = Guid.NewGuid(),
                ContentItemId = contentId
            };
            _db.ContentDrafts.Add(item.Draft);
        }

        item.Draft.XText = request.XText;
        item.Draft.WebTitle = request.WebTitle;
        item.Draft.WebBody = request.WebBody;
        item.Draft.MobileSummary = request.MobileSummary;
        item.Draft.PushTitle = request.PushTitle;
        item.Draft.PushBody = request.PushBody;
        item.Draft.HashtagsCsv = request.HashtagsCsv;
        item.Draft.MentionsCsv = request.MentionsCsv;
        
        // Channel toggles (only update if explicitly provided)
        if (request.PublishToWeb.HasValue) item.Draft.PublishToWeb = request.PublishToWeb.Value;
        if (request.PublishToMobile.HasValue) item.Draft.PublishToMobile = request.PublishToMobile.Value;
        if (request.PublishToX.HasValue) item.Draft.PublishToX = request.PublishToX.Value;
        
        item.Draft.UpdatedAtUtc = DateTime.UtcNow;
        item.Draft.UpdatedByUserId = userId;

        // Update content item editorial fields
        item.EditorialNote = request.EditorialNote;
        item.LastEditedByUserId = userId;
        item.LastEditedAtUtc = DateTime.UtcNow;

        // Create revision and update current version
        var versionNo = await GetNextVersionNoAsync(contentId);
        var revision = CreateRevision(item, item.Draft, userId, versionNo, RevisionActionTypes.DraftSaved);
        _db.ContentRevisions.Add(revision);
        
        // Update current version on content item
        item.CurrentVersionNo = versionNo;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Draft saved for content {ContentId} by user {UserId}, version {Version}", 
            contentId, userId, versionNo);

        return new SaveDraftResponse(
            new EditorialDraftDto(
                item.Draft.Id,
                item.Draft.XText,
                item.Draft.WebTitle,
                item.Draft.WebBody,
                item.Draft.MobileSummary,
                item.Draft.PushTitle,
                item.Draft.PushBody,
                item.Draft.HashtagsCsv,
                item.Draft.MentionsCsv,
                item.Draft.PublishToWeb,
                item.Draft.PublishToMobile,
                item.Draft.PublishToX,
                item.Draft.UpdatedAtUtc,
                item.Draft.UpdatedByUserId
            ),
            versionNo
        );
    }

    /// <summary>
    /// Approve content item
    /// </summary>
    public async Task<bool> ApproveAsync(Guid contentId, Guid userId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null) return false;

        item.Status = ContentStatuses.ReadyToPublish;
        item.DecidedAtUtc = DateTime.UtcNow;
        item.LastEditedByUserId = userId;
        item.LastEditedAtUtc = DateTime.UtcNow;
        item.RejectionReason = null; // Clear any previous rejection

        var versionNo = await GetNextVersionNoAsync(contentId);
        var revision = CreateRevision(item, item.Draft, userId, versionNo, RevisionActionTypes.Approved);
        _db.ContentRevisions.Add(revision);
        
        // Update version and publish origin
        item.CurrentVersionNo = versionNo;
        item.PublishOrigin = PublishOrigins.Editorial;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Content {ContentId} v{Version} approved by user {UserId}", contentId, versionNo, userId);
        return true;
    }

    /// <summary>
    /// Reject content item
    /// </summary>
    public async Task<bool> RejectAsync(Guid contentId, string reason, Guid userId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null) return false;

        item.Status = ContentStatuses.Rejected;
        item.RejectionReason = reason;
        item.LastEditedByUserId = userId;
        item.LastEditedAtUtc = DateTime.UtcNow;

        var versionNo = await GetNextVersionNoAsync(contentId);
        var revision = CreateRevision(item, item.Draft, userId, versionNo, RevisionActionTypes.Rejected);
        _db.ContentRevisions.Add(revision);
        
        // Update current version
        item.CurrentVersionNo = versionNo;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Content {ContentId} v{Version} rejected by user {UserId}: {Reason}", 
            contentId, versionNo, userId, reason);
        return true;
    }

    /// <summary>
    /// Schedule content item for future publishing
    /// </summary>
    public async Task<bool> ScheduleAsync(Guid contentId, DateTime scheduledAtUtc, Guid userId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null) return false;

        item.Status = ContentStatuses.Scheduled;
        item.ScheduledAtUtc = scheduledAtUtc;
        item.LastEditedByUserId = userId;
        item.LastEditedAtUtc = DateTime.UtcNow;
        item.RejectionReason = null; // Clear any previous rejection

        var versionNo = await GetNextVersionNoAsync(contentId);
        var revision = CreateRevision(item, item.Draft, userId, versionNo, RevisionActionTypes.Scheduled);
        _db.ContentRevisions.Add(revision);
        
        // Update version and publish origin
        item.CurrentVersionNo = versionNo;
        item.PublishOrigin = PublishOrigins.Editorial;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Content {ContentId} v{Version} scheduled for {ScheduledAt} by user {UserId}", 
            contentId, versionNo, scheduledAtUtc, userId);
        return true;
    }

    /// <summary>
    /// Correct a published content item and re-publish
    /// </summary>
    public async Task<CorrectionResult?> CorrectAsync(Guid contentId, SaveDraftRequest request, Guid userId)
    {
        var item = await _db.ContentItems
            .Include(c => c.Draft)
            .FirstOrDefaultAsync(c => c.Id == contentId);

        if (item == null) 
        {
            return new CorrectionResult { Success = false, Error = "Content item not found" };
        }

        if (item.Status != ContentStatuses.Published)
        {
            return new CorrectionResult 
            { 
                Success = false, 
                Error = $"Correction only allowed for Published content. Current status: {item.Status}" 
            };
        }

        // Upsert draft with corrections
        if (item.Draft == null)
        {
            item.Draft = new ContentDraft
            {
                Id = Guid.NewGuid(),
                ContentItemId = contentId
            };
            _db.ContentDrafts.Add(item.Draft);
        }

        // Update draft fields
        if (request.XText != null) item.Draft.XText = request.XText;
        if (request.WebTitle != null) item.Draft.WebTitle = request.WebTitle;
        if (request.WebBody != null) item.Draft.WebBody = request.WebBody;
        if (request.MobileSummary != null) item.Draft.MobileSummary = request.MobileSummary;
        if (request.PushTitle != null) item.Draft.PushTitle = request.PushTitle;
        if (request.PushBody != null) item.Draft.PushBody = request.PushBody;
        if (request.HashtagsCsv != null) item.Draft.HashtagsCsv = request.HashtagsCsv;
        if (request.MentionsCsv != null) item.Draft.MentionsCsv = request.MentionsCsv;
        
        // Channel toggles
        if (request.PublishToWeb.HasValue) item.Draft.PublishToWeb = request.PublishToWeb.Value;
        if (request.PublishToMobile.HasValue) item.Draft.PublishToMobile = request.PublishToMobile.Value;
        if (request.PublishToX.HasValue) item.Draft.PublishToX = request.PublishToX.Value;
        
        item.Draft.UpdatedAtUtc = DateTime.UtcNow;
        item.Draft.UpdatedByUserId = userId;

        // Update editorial note (can include correction note)
        if (request.EditorialNote != null)
        {
            item.EditorialNote = request.EditorialNote;
        }
        item.LastEditedByUserId = userId;
        item.LastEditedAtUtc = DateTime.UtcNow;

        // Create revision with new version
        var versionNo = await GetNextVersionNoAsync(contentId);
        var revision = CreateRevision(item, item.Draft, userId, versionNo, RevisionActionTypes.Corrected);
        _db.ContentRevisions.Add(revision);
        
        // Update current version
        item.CurrentVersionNo = versionNo;
        item.PublishOrigin = PublishOrigins.Editorial;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Content {ContentId} corrected to v{Version} by user {UserId}", 
            contentId, versionNo, userId);

        return new CorrectionResult
        {
            Success = true,
            VersionNo = versionNo
        };
    }

    /// <summary>
    /// Get the current version number for a content item
    /// </summary>
    public async Task<int> GetCurrentVersionNoAsync(Guid contentId)
    {
        var item = await _db.ContentItems.FindAsync(contentId);
        return item?.CurrentVersionNo ?? 1;
    }

    /// <summary>
    /// Create default draft for a new content item
    /// </summary>
    public ContentDraft CreateDefaultDraft(ContentItem item)
    {
        return new ContentDraft
        {
            Id = Guid.NewGuid(),
            ContentItemId = item.Id,
            XText = TruncateText(item.Title, 280),
            WebTitle = item.Title,
            WebBody = item.BodyText,
            MobileSummary = TruncateText(item.Summary ?? item.BodyText, 200),
            PushTitle = TruncateText(item.Title, 100),
            PushBody = TruncateText(item.Summary ?? item.BodyText, 200),
            PublishToWeb = true,
            PublishToMobile = true,
            PublishToX = true,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<int> GetNextVersionNoAsync(Guid contentItemId)
    {
        var maxVersion = await _db.ContentRevisions
            .Where(r => r.ContentItemId == contentItemId)
            .MaxAsync(r => (int?)r.VersionNo) ?? 0;
        return maxVersion + 1;
    }

    private static ContentRevision CreateRevision(
        ContentItem item, 
        ContentDraft? draft, 
        Guid userId, 
        int versionNo, 
        string actionType)
    {
        var snapshot = new RevisionSnapshot
        {
            Status = item.Status,
            DecisionType = item.DecisionType,
            ScheduledAtUtc = item.ScheduledAtUtc,
            EditorialNote = item.EditorialNote,
            RejectionReason = item.RejectionReason,
            Draft = draft != null ? new DraftSnapshot
            {
                XText = draft.XText,
                WebTitle = draft.WebTitle,
                WebBody = draft.WebBody,
                MobileSummary = draft.MobileSummary,
                PushTitle = draft.PushTitle,
                PushBody = draft.PushBody,
                HashtagsCsv = draft.HashtagsCsv,
                MentionsCsv = draft.MentionsCsv,
                PublishToWeb = draft.PublishToWeb,
                PublishToMobile = draft.PublishToMobile,
                PublishToX = draft.PublishToX
            } : null
        };

        return new ContentRevision
        {
            Id = Guid.NewGuid(),
            ContentItemId = item.Id,
            VersionNo = versionNo,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            ActionType = actionType
        };
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

