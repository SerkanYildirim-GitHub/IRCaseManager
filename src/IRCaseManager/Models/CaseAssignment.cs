namespace IRCaseManager.Models;

public class CaseAssignment
{
    public int CaseId { get; set; }

    public Case? Case { get; set; }

    public int ApplicationUserId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    public int AssignedById { get; set; }
}
