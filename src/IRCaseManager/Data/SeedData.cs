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
        await EnsureDevelopmentLoginHardeningSchemaAsync(db);
        await EnsureDevelopmentCaseAssignmentHistorySchemaAsync(db);
        await SeedRolesAsync(db);
        await SeedDevelopmentTestUsersAsync(db, logger);
    }

    private static async Task EnsureDevelopmentLoginHardeningSchemaAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        var existingColumns = await GetUserColumnNamesAsync(db);
        var columnsToAdd = new Dictionary<string, string>
        {
            [nameof(ApplicationUser.FailedLoginAttempts)] = "INTEGER NOT NULL DEFAULT 0",
            [nameof(ApplicationUser.LockoutEndUtc)] = "TEXT NULL",
            [nameof(ApplicationUser.LastFailedLoginUtc)] = "TEXT NULL"
        };

        foreach (var column in columnsToAdd)
        {
            if (!existingColumns.Contains(column.Key))
            {
                await ExecuteSchemaCommandAsync(db, $"ALTER TABLE Users ADD COLUMN {column.Key} {column.Value}");
            }
        }
    }

    private static async Task ExecuteSchemaCommandAsync(AppDbContext db, string commandText)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();

        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<HashSet<string>> GetUserColumnNamesAsync(AppDbContext db)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Users')";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        if (shouldClose)
        {
            await connection.CloseAsync();
        }

        return columns;
    }

    private static async Task EnsureDevelopmentCaseAssignmentHistorySchemaAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        await ExecuteSchemaCommandAsync(db, """
            CREATE TABLE IF NOT EXISTS CaseAssignmentHistories (
                Id INTEGER NOT NULL CONSTRAINT PK_CaseAssignmentHistories PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                ActionType TEXT NOT NULL,
                FromUserId INTEGER NULL,
                FromTeam TEXT NULL,
                ToUserId INTEGER NULL,
                ToTeam TEXT NULL,
                PerformedByUserId INTEGER NULL,
                Reason TEXT NULL,
                OccurredUtc TEXT NOT NULL,
                CONSTRAINT FK_CaseAssignmentHistories_Cases_CaseId FOREIGN KEY (CaseId) REFERENCES Cases (Id) ON DELETE CASCADE,
                CONSTRAINT FK_CaseAssignmentHistories_Users_FromUserId FOREIGN KEY (FromUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_CaseAssignmentHistories_Users_ToUserId FOREIGN KEY (ToUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_CaseAssignmentHistories_Users_PerformedByUserId FOREIGN KEY (PerformedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT
            )
            """);

        await ExecuteSchemaCommandAsync(db, "CREATE INDEX IF NOT EXISTS IX_CaseAssignmentHistories_CaseId_OccurredUtc ON CaseAssignmentHistories (CaseId, OccurredUtc)");
        await ExecuteSchemaCommandAsync(db, "CREATE INDEX IF NOT EXISTS IX_CaseAssignmentHistories_FromUserId ON CaseAssignmentHistories (FromUserId)");
        await ExecuteSchemaCommandAsync(db, "CREATE INDEX IF NOT EXISTS IX_CaseAssignmentHistories_ToUserId ON CaseAssignmentHistories (ToUserId)");
        await ExecuteSchemaCommandAsync(db, "CREATE INDEX IF NOT EXISTS IX_CaseAssignmentHistories_PerformedByUserId ON CaseAssignmentHistories (PerformedByUserId)");
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
