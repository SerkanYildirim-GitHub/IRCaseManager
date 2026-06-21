using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class TimelineEntry
{
    public int Id { get; set; }

    public int CaseId { get; set; }

    public Case? Case { get; set; }

    public DateTimeOffset EventTime { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(256)]
    public string Source { get; set; } = string.Empty;

    public int CreatedById { get; set; }

    public ApplicationUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
