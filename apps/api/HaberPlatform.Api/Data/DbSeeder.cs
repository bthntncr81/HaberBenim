using HaberPlatform.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Data;

public static class DbSeeder
{
    private static readonly string[] DefaultRoles = ["Admin", "Editor", "SocialMedia"];
    
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        // Seed roles
        foreach (var roleName in DefaultRoles)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == roleName))
            {
                db.Roles.Add(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName
                });
                logger.LogInformation("Created role: {RoleName}", roleName);
            }
        }
        await db.SaveChangesAsync();

        // Seed default admin user
        const string adminEmail = "admin@local";
        const string adminPassword = "Admin123!";
        const string adminDisplayName = "System Admin";

        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var passwordHasher = new PasswordHasher<string>();
            var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = passwordHasher.HashPassword(adminEmail, adminPassword),
                DisplayName = adminDisplayName,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            adminUser.UserRoles.Add(new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            });

            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            logger.LogInformation("Created default admin user: {Email}", adminEmail);
        }

        // Seed default rules if none exist
        await SeedRulesAsync(db, logger);

        // Seed X integration system settings (Sprint 9)
        await SeedXSettingsAsync(db, logger);
    }

    private static async Task SeedRulesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Rules.AnyAsync())
        {
            return; // Rules already exist
        }

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@local");
        var adminUserId = adminUser?.Id;

        // Rule 1: High trust auto-publish
        var highTrustRule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "High trust auto-publish",
            IsEnabled = true,
            Priority = 100,
            DecisionType = DecisionTypes.AutoPublish,
            MinTrustLevel = 3, // High trust sources
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = adminUserId
        };

        // Rule 2: Blacklist keywords - Block
        var blacklistRule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Blacklist keywords",
            IsEnabled = true,
            Priority = 1000, // Highest priority
            DecisionType = DecisionTypes.Block,
            KeywordsIncludeCsv = "casino,bet,xxx,poker,bahis,kumar",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = adminUserId
        };

        db.Rules.AddRange(highTrustRule, blacklistRule);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded default rules: {Rule1}, {Rule2}", 
            highTrustRule.Name, blacklistRule.Name);
    }

    private static async Task SeedXSettingsAsync(AppDbContext db, ILogger logger)
    {
        var xSettings = new Dictionary<string, string>
        {
            ["X_CLIENT_ID"] = "",
            ["X_CLIENT_SECRET"] = "",
            ["X_REDIRECT_URI"] = "http://localhost:5078/api/v1/integrations/x/callback",
            ["X_APP_BEARER_TOKEN"] = "",
            ["X_API_BASE_URL"] = "https://api.x.com"
        };

        foreach (var (key, defaultValue) in xSettings)
        {
            if (!await db.SystemSettings.AnyAsync(s => s.Key == key))
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = defaultValue,
                    CreatedAtUtc = DateTime.UtcNow
                });
                logger.LogInformation("Created system setting: {Key}", key);
            }
        }

        await db.SaveChangesAsync();
    }
}
