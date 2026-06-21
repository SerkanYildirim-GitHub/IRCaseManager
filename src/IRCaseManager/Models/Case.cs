using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class Case
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string CaseId { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    public CaseSeverity Severity { get; set; }

    public CaseType CaseType { get; set; }

    public CaseStatus Status { get; set; } = CaseStatus.New;

    [StringLength(80)]
    public string? SourceReference { get; set; }

    [Required, StringLength(100)]
    public string AssignedTeam { get; set; } = string.Empty;

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ClosedAt { get; set; }

    [Required, StringLength(4000)]
    public string InitialSummary { get; set; } = string.Empty;

    [StringLength(256)]
    public string Visibility { get; set; } = "Default";

    public int CreatedById { get; set; }

    public ApplicationUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? UpdatedById { get; set; }

    public ApplicationUser? UpdatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<CaseAssignment> Assignments { get; set; } = [];

    public ICollection<CaseAssignmentHistory> AssignmentHistory { get; set; } = [];

    public ICollection<EvidenceMetadata> EvidenceItems { get; set; } = [];

    public ICollection<TimelineEntry> TimelineEntries { get; set; } = [];

    public ICollection<CasePlaybookStep> PlaybookSteps { get; set; } = [];
}
