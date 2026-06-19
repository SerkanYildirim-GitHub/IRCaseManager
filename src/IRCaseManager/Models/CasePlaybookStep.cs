using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class CasePlaybookStep
{
    public int Id { get; set; }

    public int CaseId { get; set; }

    public Case? Case { get; set; }

    [Required, StringLength(120)]
    public string StepKey { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int? CompletedById { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int UpdatedById { get; set; }
}
