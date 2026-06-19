using IRCaseManager.Models;
using IRCaseManager.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Data;

public static class SeedData
{
    public static async Task InitializeDevelopmentAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentSeed");

        await db.Database.EnsureCreatedAsync();
        await SeedRolesAsync(db);
        await SeedDevelopmentTestUsersAsync(db, logger);
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        var existingRoles = await db.Roles.Select(role => role.Name).ToListAsync();

        foreach (var roleName in RoleNames.All.Except(existingRoles))
        {
            db.Roles.Add(new Role
            {
                Name = roleName,
                Description = roleName switch
                {
                    RoleNames.Admin => "Full local administration access.",
                    RoleNames.AnalystLevel2 => "Senior analyst access, including case reopen.",
                    RoleNames.AnalystLevel1 => "Analyst access for visible and assigned case work.",
                    RoleNames.Auditor => "Read-only audit access.",
                    _ => "Application role."
                }
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDevelopmentTestUsersAsync(AppDbContext db, ILogger logger)
    {
        var testPassword = Environment.GetEnvironmentVariable("IRCASEMANAGER_DEV_TEST_PASSWORD");
        if (string.IsNullOrWhiteSpace(testPassword))
        {
            testPassword = Environment.GetEnvironmentVariable("IRCASEMANAGER_DEV_ADMIN_PASSWORD");
        }

        if (string.IsNullOrWhiteSpace(testPassword))
        {
            logger.LogWarning("Development test user seed skipped. Set IRCASEMANAGER_DEV_TEST_PASSWORD or IRCASEMANAGER_DEV_ADMIN_PASSWORD before running in Development.");
            return;
        }

        var roles = await db.Roles.ToDictionaryAsync(role => role.Name, role => role.Id);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var testUsers = new[]
        {
            new DevelopmentTestUser("admin.local", "admin.local@example.invalid", RoleNames.Admin),
            new DevelopmentTestUser("analyst.l1.local", "analyst.l1.local@example.invalid", RoleNames.AnalystLevel1),
            new DevelopmentTestUser("analyst.l2.local", "analyst.l2.local@example.invalid", RoleNames.AnalystLevel2),
            new DevelopmentTestUser("auditor.local", "auditor.local@example.invalid", RoleNames.Auditor)
        };

        foreach (var testUser in testUsers)
        {
            if (!roles.TryGetValue(testUser.RoleName, out var roleId))
            {
                continue;
            }

            var existingUser = await db.Users.SingleOrDefaultAsync(user => user.UserName == testUser.UserName);
            if (existingUser is null)
            {
                var user = new ApplicationUser
                {
                    UserName = testUser.UserName,
                    Email = testUser.Email,
                    RoleId = roleId,
                    IsActive = true
                };

                user.PasswordHash = passwordHasher.HashPassword(user, testPassword);
                db.Users.Add(user);
                AddDevelopmentUserAuditLog(db, user.UserName, "DevelopmentTestUserSeeded", "Development-only local test user created.");
                continue;
            }

            existingUser.Email = testUser.Email;
            existingUser.RoleId = roleId;
            existingUser.IsActive = true;

            if (await ShouldUpgradeOldAnalystPlaceholderPasswordAsync(db, existingUser.UserName))
            {
                existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, testPassword);
                AddDevelopmentUserAuditLog(db, existingUser.UserName, "DevelopmentTestUserPasswordSet", "Development-only local test user password hash set from environment configuration.");
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Development-only local test users are available for role access testing.");
    }

    private static async Task<bool> ShouldUpgradeOldAnalystPlaceholderPasswordAsync(AppDbContext db, string userName)
    {
        var wasOldPlaceholder = await db.AuditLogs.AnyAsync(auditLog =>
            auditLog.Action == "DevelopmentAnalystSeeded" &&
            auditLog.EntityId == userName);

        if (!wasOldPlaceholder)
        {
            return false;
        }

        var alreadyUpgraded = await db.AuditLogs.AnyAsync(auditLog =>
            auditLog.Action == "DevelopmentTestUserPasswordSet" &&
            auditLog.EntityId == userName);

        return !alreadyUpgraded;
    }

    private static void AddDevelopmentUserAuditLog(AppDbContext db, string userName, string action, string summary)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = nameof(ApplicationUser),
            EntityId = userName,
            Summary = summary
        });
    }

    private sealed record DevelopmentTestUser(string UserName, string Email, string RoleName);
}
