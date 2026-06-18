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
}
