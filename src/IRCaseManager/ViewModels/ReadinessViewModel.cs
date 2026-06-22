using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.ViewModels;

public class ReadinessViewModel
{
    public bool CanEdit { get; init; }

    public ReadinessScoreViewModel Overall { get; init; } = new();

    public IReadOnlyList<ReadinessSectionViewModel> Sections { get; init; } = [];

    public IReadOnlyList<string> AnswerOptions { get; init; } = [];
}

public class ReadinessScoreViewModel
{
    public int? Percentage { get; init; }

    public string StatusLabel { get; init; } = "Not Started";

    public string StatusClass { get; init; } = "not-started";

    public int UnansweredCount { get; init; }

    public int HighRiskGapCount { get; init; }

    public int ConfirmedGapCount { get; init; }

    public int UnverifiedGapCount { get; init; }

    public int ApplicableAnsweredCount { get; init; }

    public int NotApplicableCount { get; init; }

    public string? LastUpdatedBy { get; init; }

    public DateTimeOffset? LastUpdatedAtUtc { get; init; }

    public int FillPercent => Percentage ?? 0;
}

public class ReadinessSectionViewModel
{
    public string SectionName { get; init; } = string.Empty;

    public ReadinessScoreViewModel Score { get; init; } = new();

    public IReadOnlyList<ReadinessQuestionViewModel> Questions { get; init; } = [];
}

public class ReadinessQuestionViewModel
{
    public string SectionName { get; init; } = string.Empty;

    public string QuestionKey { get; init; } = string.Empty;

    public string QuestionText { get; init; } = string.Empty;

    public string? AnswerValue { get; init; }

    public string? Comment { get; init; }

    public string? UpdatedByUserName { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

public class ReadinessSaveViewModel
{
    public List<ReadinessAnswerInputViewModel> Answers { get; init; } = [];
}

public class ReadinessAnswerInputViewModel
{
    [Required, StringLength(120)]
    public string QuestionKey { get; set; } = string.Empty;

    [StringLength(32)]
    public string? AnswerValue { get; set; }

    [StringLength(1500)]
    public string? Comment { get; set; }

    public void Trim()
    {
        QuestionKey = QuestionKey.Trim();
        AnswerValue = string.IsNullOrWhiteSpace(AnswerValue) ? null : AnswerValue.Trim();
        Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim();
    }
}
