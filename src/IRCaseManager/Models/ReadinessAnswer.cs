using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class ReadinessAnswer
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string QuestionKey { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string SectionName { get; set; } = string.Empty;

    [StringLength(32)]
    public string? AnswerValue { get; set; }

    [StringLength(1500)]
    public string? Comment { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }

    public ApplicationUser? UpdatedByUser { get; set; }
}
