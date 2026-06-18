using IRCaseManager.Models;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseAssignment> CaseAssignments => Set<CaseAssignment>();
    public DbSet<EvidenceMetadata> EvidenceMetadata => Set<EvidenceMetadata>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(user => user.UserName)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(role => role.Name)
            .IsUnique();

        modelBuilder.Entity<Case>()
            .HasIndex(irCase => irCase.CaseId)
            .IsUnique();

        modelBuilder.Entity<CaseAssignment>()
            .HasKey(assignment => new { assignment.CaseId, assignment.ApplicationUserId });

        modelBuilder.Entity<CaseAssignment>()
            .HasOne(assignment => assignment.Case)
            .WithMany(irCase => irCase.Assignments)
            .HasForeignKey(assignment => assignment.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CaseAssignment>()
            .HasOne(assignment => assignment.ApplicationUser)
            .WithMany(user => user.CaseAssignments)
            .HasForeignKey(assignment => assignment.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Case>()
            .HasOne(irCase => irCase.CreatedBy)
            .WithMany()
            .HasForeignKey(irCase => irCase.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Case>()
            .HasOne(irCase => irCase.UpdatedBy)
            .WithMany()
            .HasForeignKey(irCase => irCase.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
