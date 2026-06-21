using IRCaseManager.Models;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseAssignment> CaseAssignments => Set<CaseAssignment>();
    public DbSet<CaseAssignmentHistory> CaseAssignmentHistories => Set<CaseAssignmentHistory>();
    public DbSet<EvidenceMetadata> EvidenceMetadata => Set<EvidenceMetadata>();
    public DbSet<CasePlaybookStep> CasePlaybookSteps => Set<CasePlaybookStep>();
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

        modelBuilder.Entity<CasePlaybookStep>()
            .HasIndex(step => new { step.CaseId, step.StepKey })
            .IsUnique();

        modelBuilder.Entity<CaseAssignmentHistory>()
            .HasIndex(history => new { history.CaseId, history.OccurredUtc });

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

        modelBuilder.Entity<CasePlaybookStep>()
            .HasOne(step => step.Case)
            .WithMany(irCase => irCase.PlaybookSteps)
            .HasForeignKey(step => step.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CaseAssignmentHistory>()
            .HasOne(history => history.Case)
            .WithMany(irCase => irCase.AssignmentHistory)
            .HasForeignKey(history => history.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CaseAssignmentHistory>()
            .HasOne(history => history.FromUser)
            .WithMany()
            .HasForeignKey(history => history.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CaseAssignmentHistory>()
            .HasOne(history => history.ToUser)
            .WithMany()
            .HasForeignKey(history => history.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CaseAssignmentHistory>()
            .HasOne(history => history.PerformedByUser)
            .WithMany()
            .HasForeignKey(history => history.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
