using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class CaseAssignmentHistory
{
    public int Id { get; set; }

    public int CaseId { get; set; }

    public Case? Case { get; set; }

    [Required, StringLength(40)]
    public string ActionType { get; set; } = string.Empty;

    public int? FromUserId { get; set; }

    public ApplicationUser? FromUser { get; set; }

    [StringLength(100)]
    public string? FromTeam { get; set; }

    public int? ToUserId { get; set; }

    public ApplicationUser? ToUser { get; set; }

    [StringLength(100)]
    public string? ToTeam { get; set; }

    public int? PerformedByUserId { get; set; }

    public ApplicationUser? PerformedByUser { get; set; }

    [StringLength(512)]
    public string? Reason { get; set; }

    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}
