using Microsoft.EntityFrameworkCore;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Auth & System
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    
    // Content & Sources
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<ContentMedia> ContentMedia => Set<ContentMedia>();
    public DbSet<ContentDuplicate> ContentDuplicates => Set<ContentDuplicate>();
    public DbSet<ContentDraft> ContentDrafts => Set<ContentDraft>();
    public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>();
    
    // Media Assets (Sprint 10)
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ContentMediaLink> ContentMediaLinks => Set<ContentMediaLink>();
    
    // Rules
    public DbSet<Rule> Rules => Set<Rule>();
    
    // Publishing
    public DbSet<PublishedContent> PublishedContents => Set<PublishedContent>();
    public DbSet<PublishJob> PublishJobs => Set<PublishJob>();
    public DbSet<ChannelPublishLog> ChannelPublishLogs => Set<ChannelPublishLog>();
    
    // Reporting (Sprint 7)
    public DbSet<DailyReportRun> DailyReportRuns => Set<DailyReportRun>();

    // Breaking, Alerts, Health (Sprint 8)
    public DbSet<SourceIngestionHealth> SourceIngestionHealths => Set<SourceIngestionHealth>();
    public DbSet<AdminAlert> AdminAlerts => Set<AdminAlert>();

    // X Integration (Sprint 9)
    public DbSet<XIntegrationConnection> XIntegrationConnections => Set<XIntegrationConnection>();
    public DbSet<XSourceState> XSourceStates => Set<XSourceState>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();
    
    // Instagram Integration (Sprint 11)
    public DbSet<InstagramConnection> InstagramConnections => Set<InstagramConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SystemSetting
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // UserRole (many-to-many)
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.Method).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.StatusCode).IsRequired();
            entity.Property(e => e.DurationMs).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(50);
            entity.Property(e => e.UserEmail).HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        // Source
        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Identifier).HasMaxLength(100);
            entity.Property(e => e.Url).HasMaxLength(2048);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100).HasDefaultValue("Gundem");
            entity.Property(e => e.Group).HasMaxLength(100);
            entity.Property(e => e.TrustLevel).IsRequired().HasDefaultValue(50);
            entity.Property(e => e.Priority).IsRequired().HasDefaultValue(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.DefaultBehavior).IsRequired().HasMaxLength(50).HasDefaultValue("Editorial");
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            
            // Unique constraints
            entity.HasIndex(e => e.Name).IsUnique();
            // Unique URL for RSS sources (PostgreSQL syntax)
            entity.HasIndex(e => e.Url).IsUnique().HasFilter("\"Url\" IS NOT NULL AND \"Type\" = 'RSS'");
            // Unique (Type, Identifier) for X sources (PostgreSQL syntax)
            entity.HasIndex(e => new { e.Type, e.Identifier }).IsUnique().HasFilter("\"Identifier\" IS NOT NULL");
            
            // Query indexes
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => new { e.IsActive, e.Priority }).IsDescending(false, true);
            entity.HasIndex(e => new { e.Type, e.IsActive });
            entity.HasIndex(e => e.Group);
        });

        // ContentItem
        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Summary).HasMaxLength(5000);
            entity.Property(e => e.BodyText).IsRequired();
            entity.Property(e => e.CanonicalUrl).HasMaxLength(2048);
            entity.Property(e => e.Language).HasMaxLength(10);
            entity.Property(e => e.DedupHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PublishedAtUtc).IsRequired();
            entity.Property(e => e.IngestedAtUtc).IsRequired();
            
            // Decision fields
            entity.Property(e => e.DecisionType).HasMaxLength(50);
            entity.Property(e => e.DecisionReason).HasMaxLength(500);
            
            // Editorial fields
            entity.Property(e => e.EditorialNote).HasMaxLength(2000);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);

            entity.HasOne(e => e.Source)
                .WithMany(s => s.ContentItems)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DecidedByRule)
                .WithMany(r => r.DecidedContentItems)
                .HasForeignKey(e => e.DecidedByRuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.LastEditedByUser)
                .WithMany()
                .HasForeignKey(e => e.LastEditedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PublishedByUser)
                .WithMany()
                .HasForeignKey(e => e.PublishedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Version-aware publishing fields
            entity.Property(e => e.CurrentVersionNo).HasDefaultValue(1);
            entity.Property(e => e.PublishOrigin).HasMaxLength(50);

            // Breaking News fields (Sprint 8)
            entity.Property(e => e.IsBreaking).HasDefaultValue(false);
            entity.Property(e => e.BreakingPushRequired).HasDefaultValue(true);
            entity.Property(e => e.BreakingNote).HasMaxLength(500);
            entity.Property(e => e.BreakingPriority).HasDefaultValue(100);

            entity.HasOne(e => e.BreakingByUser)
                .WithMany()
                .HasForeignKey(e => e.BreakingByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Retract fields (Sprint 8)
            entity.Property(e => e.RetractReason).HasMaxLength(1000);

            entity.HasOne(e => e.RetractedByUser)
                .WithMany()
                .HasForeignKey(e => e.RetractedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indices
            entity.HasIndex(e => new { e.SourceId, e.PublishedAtUtc });
            entity.HasIndex(e => new { e.SourceId, e.ExternalId }).IsUnique();
            entity.HasIndex(e => e.DedupHash);
            entity.HasIndex(e => e.CanonicalUrl);
            entity.HasIndex(e => e.PublishedAtUtc);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.DecisionType, e.PublishedAtUtc });
            entity.HasIndex(e => new { e.Status, e.PublishedAtUtc });
            entity.HasIndex(e => new { e.IsBreaking, e.BreakingPriority, e.BreakingAtUtc })
                .IsDescending(false, true, true);
        });

        // ContentMedia
        modelBuilder.Entity<ContentMedia>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MediaType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.ThumbUrl).HasMaxLength(2048);
            entity.Property(e => e.Title).HasMaxLength(500);

            entity.HasOne(e => e.ContentItem)
                .WithMany(c => c.Media)
                .HasForeignKey(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ContentDuplicate
        modelBuilder.Entity<ContentDuplicate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Method).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DetectedAtUtc).IsRequired();

            entity.HasOne(e => e.ContentItem)
                .WithMany(c => c.Duplicates)
                .HasForeignKey(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DuplicateOfContentItem)
                .WithMany()
                .HasForeignKey(e => e.DuplicateOfContentItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ContentItemId, e.DuplicateOfContentItemId }).IsUnique();
        });

        // ContentDraft (1:1 with ContentItem)
        modelBuilder.Entity<ContentDraft>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.XText).HasMaxLength(300);
            entity.Property(e => e.WebTitle).HasMaxLength(500);
            entity.Property(e => e.WebBody);
            entity.Property(e => e.MobileSummary).HasMaxLength(500);
            entity.Property(e => e.PushTitle).HasMaxLength(100);
            entity.Property(e => e.PushBody).HasMaxLength(300);
            entity.Property(e => e.HashtagsCsv).HasMaxLength(500);
            entity.Property(e => e.MentionsCsv).HasMaxLength(500);
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            
            // Channel toggles
            entity.Property(e => e.PublishToWeb).HasDefaultValue(true);
            entity.Property(e => e.PublishToMobile).HasDefaultValue(true);
            entity.Property(e => e.PublishToX).HasDefaultValue(true);
            entity.Property(e => e.PublishToInstagram).HasDefaultValue(true);
            entity.Property(e => e.InstagramCaptionOverride).HasMaxLength(2200);

            // Media/image settings (Sprint 10)
            entity.Property(e => e.AutoGenerateImageIfMissing).HasDefaultValue(true);
            entity.Property(e => e.ImagePromptOverride).HasMaxLength(1000);
            entity.Property(e => e.ImageStylePreset).HasMaxLength(100);

            entity.HasOne(e => e.ContentItem)
                .WithOne(c => c.Draft)
                .HasForeignKey<ContentDraft>(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UpdatedByUser)
                .WithMany()
                .HasForeignKey(e => e.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ContentItemId).IsUnique();
        });

        // ContentRevision
        modelBuilder.Entity<ContentRevision>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VersionNo).IsRequired();
            entity.Property(e => e.SnapshotJson).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(50);

            entity.HasOne(e => e.ContentItem)
                .WithMany(c => c.Revisions)
                .HasForeignKey(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.ContentItemId, e.VersionNo }).IsDescending(false, true);
        });

        // Rule
        modelBuilder.Entity<Rule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.Priority).IsRequired();
            entity.Property(e => e.DecisionType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.KeywordsIncludeCsv).HasMaxLength(2000);
            entity.Property(e => e.KeywordsExcludeCsv).HasMaxLength(2000);
            entity.Property(e => e.SourceIdsCsv).HasMaxLength(4000);
            entity.Property(e => e.GroupIdsCsv).HasMaxLength(1000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => new { e.IsEnabled, e.Priority });
        });

        // PublishedContent (1:1 with ContentItem)
        modelBuilder.Entity<PublishedContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WebTitle).IsRequired().HasMaxLength(500);
            entity.Property(e => e.WebBody).IsRequired();
            entity.Property(e => e.CanonicalUrl).HasMaxLength(2048);
            entity.Property(e => e.SourceName).HasMaxLength(255);
            entity.Property(e => e.CategoryOrGroup).HasMaxLength(100);
            entity.Property(e => e.PublishedAtUtc).IsRequired();
            
            // SEO fields
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(200);

            entity.HasOne(e => e.ContentItem)
                .WithOne(c => c.PublishedContent)
                .HasForeignKey<PublishedContent>(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ContentItemId).IsUnique();
            entity.HasIndex(e => e.PublishedAtUtc).IsDescending();
            entity.HasIndex(e => e.Slug);
            entity.HasIndex(e => e.Path);

            // Compliance & Retract (Sprint 8)
            entity.Property(e => e.SourceAttributionText).HasMaxLength(500);
            entity.Property(e => e.IsRetracted).HasDefaultValue(false);
            entity.HasIndex(e => e.IsRetracted);

            // Media (Sprint 10)
            entity.Property(e => e.PrimaryImageUrl).HasMaxLength(500);
        });

        // PublishJob
        modelBuilder.Entity<PublishJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScheduledAtUtc).IsRequired();
            entity.Property(e => e.VersionNo).HasDefaultValue(1);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AttemptCount).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();

            entity.HasOne(e => e.ContentItem)
                .WithMany(c => c.PublishJobs)
                .HasForeignKey(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.Status, e.ScheduledAtUtc });
            entity.HasIndex(e => e.ContentItemId);
            entity.HasIndex(e => new { e.ContentItemId, e.VersionNo });
        });

        // ChannelPublishLog
        modelBuilder.Entity<ChannelPublishLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.VersionNo).HasDefaultValue(1);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.Property(e => e.ExternalPostId).HasMaxLength(100);

            entity.HasOne(e => e.ContentItem)
                .WithMany(c => c.PublishLogs)
                .HasForeignKey(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ContentItemId, e.Channel });
            entity.HasIndex(e => new { e.ContentItemId, e.Channel, e.VersionNo });
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        });

        // DailyReportRun (Sprint 7)
        modelBuilder.Entity<DailyReportRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReportDateLocal).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Error).HasMaxLength(2000);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ReportDateLocal);
            entity.HasIndex(e => new { e.ReportDateLocal, e.Status });
        });

        // SourceIngestionHealth (Sprint 8)
        modelBuilder.Entity<SourceIngestionHealth>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LastSuccessAtUtc).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.ConsecutiveFailures).HasDefaultValue(0);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

            entity.HasOne(e => e.Source)
                .WithOne(s => s.IngestionHealth)
                .HasForeignKey<SourceIngestionHealth>(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SourceId).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        // AdminAlert (Sprint 8)
        modelBuilder.Entity<AdminAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.IsAcknowledged).HasDefaultValue(false);

            entity.HasOne(e => e.AcknowledgedByUser)
                .WithMany()
                .HasForeignKey(e => e.AcknowledgedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.CreatedAtUtc).IsDescending();
            entity.HasIndex(e => new { e.Severity, e.IsAcknowledged });
            entity.HasIndex(e => new { e.Type, e.CreatedAtUtc });
        });

        // XIntegrationConnection (Sprint 9)
        modelBuilder.Entity<XIntegrationConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.XUserId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.XUsername).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ScopesCsv).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.AccessTokenEncrypted).IsRequired();
            entity.Property(e => e.RefreshTokenEncrypted).IsRequired();
            entity.Property(e => e.AccessTokenExpiresAtUtc).IsRequired();
            entity.Property(e => e.IsDefaultPublisher).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            entity.HasIndex(e => e.XUserId);
            entity.HasIndex(e => e.XUsername);
            entity.HasIndex(e => e.IsDefaultPublisher);
        });

        // XSourceState (Sprint 9)
        modelBuilder.Entity<XSourceState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.XUserId).HasMaxLength(50);
            entity.Property(e => e.LastSinceId).HasMaxLength(50);
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.ConsecutiveFailures).HasDefaultValue(0);

            entity.HasOne(e => e.Source)
                .WithOne(s => s.XSourceState)
                .HasForeignKey<XSourceState>(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SourceId).IsUnique();
        });

        // OAuthState (Sprint 9)
        modelBuilder.Entity<OAuthState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.State).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CodeVerifier).IsRequired().HasMaxLength(10000); // Increased for storing JSON page data
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ExpiresAtUtc).IsRequired();

            entity.HasIndex(e => e.State).IsUnique();
            entity.HasIndex(e => e.ExpiresAtUtc);
        });
        
        // InstagramConnection (Sprint 11)
        modelBuilder.Entity<InstagramConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FacebookUserId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PageId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PageName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IgUserId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IgUsername).HasMaxLength(100);
            entity.Property(e => e.ScopesCsv).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.PageAccessTokenEncrypted).IsRequired();
            entity.Property(e => e.TokenExpiresAtUtc).IsRequired();
            entity.Property(e => e.IsDefaultPublisher).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            entity.HasIndex(e => e.PageId);
            entity.HasIndex(e => e.IgUserId);
            entity.HasIndex(e => e.IsDefaultPublisher);
        });

        // MediaAsset (Sprint 10)
        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Origin).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SourceUrl).HasMaxLength(2048);
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SizeBytes).IsRequired();
            entity.Property(e => e.Width).IsRequired();
            entity.Property(e => e.Height).IsRequired();
            entity.Property(e => e.Sha256).HasMaxLength(64);
            entity.Property(e => e.AltText).HasMaxLength(500);
            entity.Property(e => e.GenerationPrompt).HasMaxLength(2000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();

            entity.HasIndex(e => e.Sha256);
            entity.HasIndex(e => e.Origin);
            entity.HasIndex(e => e.CreatedAtUtc).IsDescending();
        });

        // ContentMediaLink (Sprint 10)
        modelBuilder.Entity<ContentMediaLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsPrimary).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAtUtc).IsRequired();

            entity.HasOne(e => e.ContentItem)
                .WithMany(c => c.MediaLinks)
                .HasForeignKey(e => e.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MediaAsset)
                .WithMany(m => m.ContentLinks)
                .HasForeignKey(e => e.MediaAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ContentItemId, e.MediaAssetId }).IsUnique();
            entity.HasIndex(e => new { e.ContentItemId, e.IsPrimary });
            entity.HasIndex(e => new { e.ContentItemId, e.SortOrder });
        });
    }
}
