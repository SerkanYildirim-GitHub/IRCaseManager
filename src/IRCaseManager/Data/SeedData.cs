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
        await SeedDevelopmentAdminAsync(db, configuration, logger);
        await SeedDevelopmentAnalystPlaceholdersAsync(db);
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

    private static async Task SeedDevelopmentAdminAsync(AppDbContext db, IConfiguration configuration, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var adminPassword = Environment.GetEnvironmentVariable("IRCASEMANAGER_DEV_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = configuration["DevelopmentSeed:AdminPassword"];
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Development Admin seed skipped. Set IRCASEMANAGER_DEV_ADMIN_PASSWORD before first run to create the local development Admin user.");
            return;
        }

        var adminRole = await db.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        var admin = new ApplicationUser
        {
            UserName = "admin.local",
            Email = "admin.local@example.invalid",
            RoleId = adminRole.Id,
            IsActive = true
        };

        admin.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "DevelopmentAdminSeeded",
            EntityType = nameof(ApplicationUser),
            EntityId = admin.UserName,
            Summary = "Development-only local Admin account created."
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Development-only Admin user seeded as admin.local.");
    }

    private static async Task SeedDevelopmentAnalystPlaceholdersAsync(AppDbContext db)
    {
        var analystRoles = await db.Roles
            .Where(role => role.Name == RoleNames.AnalystLevel1 || role.Name == RoleNames.AnalystLevel2)
            .ToDictionaryAsync(role => role.Name, role => role.Id);

        var existingUserNames = await db.Users
            .Where(user => user.UserName == "analyst.l1.local" || user.UserName == "analyst.l2.local")
            .Select(user => user.UserName)
            .ToListAsync();

        var placeholderUsers = new List<ApplicationUser>();

        if (!existingUserNames.Contains("analyst.l1.local") &&
            analystRoles.TryGetValue(RoleNames.AnalystLevel1, out var analystLevel1RoleId))
        {
            placeholderUsers.Add(CreateDevelopmentPlaceholderUser(
                "analyst.l1.local",
                "analyst.l1.local@example.invalid",
                analystLevel1RoleId));
        }

        if (!existingUserNames.Contains("analyst.l2.local") &&
            analystRoles.TryGetValue(RoleNames.AnalystLevel2, out var analystLevel2RoleId))
        {
            placeholderUsers.Add(CreateDevelopmentPlaceholderUser(
                "analyst.l2.local",
                "analyst.l2.local@example.invalid",
                analystLevel2RoleId));
        }

        if (placeholderUsers.Count == 0)
        {
            return;
        }

        foreach (var placeholderUser in placeholderUsers)
        {
            db.Users.Add(placeholderUser);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "DevelopmentAnalystSeeded",
                EntityType = nameof(ApplicationUser),
                EntityId = placeholderUser.UserName,
                Summary = "Development-only analyst placeholder user created for local case assignment testing."
            });
        }

        await db.SaveChangesAsync();
    }

    private static ApplicationUser CreateDevelopmentPlaceholderUser(string userName, string email, int roleId)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            RoleId = roleId,
            IsActive = true
        };

        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, Guid.NewGuid().ToString("N"));
        return user;
    }
}
