using IRCaseManager.Data;
using IRCaseManager.Extensions;
using IRCaseManager.Models;
using IRCaseManager.Security;
using IRCaseManager.Services;
using IRCaseManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReadOnlyAccess)]
public class ResourcesController(AppDbContext db, ReadinessQuestionCatalog questionCatalog) : Controller
{
    private const string AnswerYes = "Yes";
    private const string AnswerPartial = "Partial";
    private const string AnswerNo = "No";
    private const string AnswerNotApplicable = "Not Applicable";

    private static readonly IReadOnlyList<string> AnswerOptions =
    [
        AnswerYes,
        AnswerPartial,
        AnswerNo,
        AnswerNotApplicable
    ];

    [HttpGet]
    public async Task<IActionResult> Readiness()
    {
        return View(await BuildReadinessViewModelAsync(CanEditReadiness()));
    }

    [HttpPost]
    public async Task<IActionResult> Readiness(ReadinessSaveViewModel model)
    {
        if (!CanEditReadiness())
        {
            return Forbid();
        }

        foreach (var answer in model.Answers)
        {
            answer.Trim();
        }

        var questionsByKey = questionCatalog
            .GetQuestions()
            .ToDictionary(question => question.QuestionKey, StringComparer.Ordinal);

        var submittedKeys = model.Answers.Select(answer => answer.QuestionKey).ToList();
        if (submittedKeys.Count != submittedKeys.Distinct(StringComparer.Ordinal).Count())
        {
            return BadRequest("Duplicate readiness question keys are not allowed.");
        }

        if (submittedKeys.Any(questionKey => !questionsByKey.ContainsKey(questionKey)))
        {
            return BadRequest("Unknown readiness question key.");
        }

        foreach (var answer in model.Answers)
        {
            if (!IsAllowedAnswerValue(answer.AnswerValue))
            {
                ModelState.AddModelError(string.Empty, "One or more readiness answers has an invalid value.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildReadinessViewModelAsync(CanEditReadiness()));
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        var existingAnswers = await db.ReadinessAnswers
            .Where(answer => submittedKeys.Contains(answer.QuestionKey))
            .ToDictionaryAsync(answer => answer.QuestionKey, StringComparer.Ordinal);

        var changes = 0;
        foreach (var input in model.Answers)
        {
            var definition = questionsByKey[input.QuestionKey];
            var answerValue = NormalizeAnswerValue(input.AnswerValue);
            var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();

            if (!existingAnswers.TryGetValue(input.QuestionKey, out var answer))
            {
                if (answerValue is null && comment is null)
                {
                    continue;
                }

                answer = new ReadinessAnswer
                {
                    QuestionKey = definition.QuestionKey,
                    SectionName = definition.SectionName
                };

                db.ReadinessAnswers.Add(answer);
            }

            if (string.Equals(answer.AnswerValue, answerValue, StringComparison.Ordinal) &&
                string.Equals(answer.Comment, comment, StringComparison.Ordinal))
            {
                answer.SectionName = definition.SectionName;
                continue;
            }

            var oldAnswerValue = answer.AnswerValue;
            answer.SectionName = definition.SectionName;
            answer.AnswerValue = answerValue;
            answer.Comment = comment;
            answer.UpdatedAtUtc = now;
            answer.UpdatedByUserId = userId.Value;
            changes++;

            db.AuditLogs.Add(new AuditLog
            {
                ApplicationUserId = userId.Value,
                Action = "ReadinessAnswerUpdated",
                EntityType = nameof(ReadinessAnswer),
                EntityId = definition.QuestionKey,
                Summary = $"Readiness answer changed from '{oldAnswerValue ?? "Unanswered"}' to '{answerValue ?? "Unanswered"}'.",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = changes == 1
            ? "Updated 1 readiness answer."
            : $"Updated {changes} readiness answers.";

        return RedirectToAction(nameof(Readiness));
    }

    private async Task<ReadinessViewModel> BuildReadinessViewModelAsync(bool canEdit)
    {
        var definitions = questionCatalog.GetQuestions();
        var answers = await db.ReadinessAnswers
            .AsNoTracking()
            .Include(answer => answer.UpdatedByUser)
            .ToDictionaryAsync(answer => answer.QuestionKey, StringComparer.Ordinal);

        var questionViewModels = definitions
            .OrderBy(question => question.DisplayOrder)
            .Select(question =>
            {
                answers.TryGetValue(question.QuestionKey, out var answer);
                return new ReadinessQuestionViewModel
                {
                    SectionName = question.SectionName,
                    QuestionKey = question.QuestionKey,
                    QuestionText = question.QuestionText,
                    AnswerValue = NormalizeAnswerValue(answer?.AnswerValue),
                    Comment = answer?.Comment,
                    UpdatedAtUtc = answer?.UpdatedAtUtc,
                    UpdatedByUserName = answer?.UpdatedByUser?.UserName
                };
            })
            .ToList();

        var sections = questionViewModels
            .GroupBy(question => question.SectionName)
            .Select(group => new ReadinessSectionViewModel
            {
                SectionName = group.Key,
                Questions = group.ToList(),
                Score = CalculateScore(group)
            })
            .ToList();

        return new ReadinessViewModel
        {
            CanEdit = canEdit,
            AnswerOptions = AnswerOptions,
            Sections = sections,
            Overall = CalculateScore(questionViewModels)
        };
    }

    private bool CanEditReadiness()
    {
        return User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.AnalystLevel2);
    }

    private static ReadinessScoreViewModel CalculateScore(IEnumerable<ReadinessQuestionViewModel> questions)
    {
        var questionList = questions.ToList();
        var applicableQuestions = questionList
            .Where(question => question.AnswerValue != AnswerNotApplicable)
            .ToList();
        var notApplicableCount = questionList.Count(question => question.AnswerValue == AnswerNotApplicable);
        var unansweredCount = questionList.Count(question => string.IsNullOrWhiteSpace(question.AnswerValue));
        var confirmedGapCount = questionList.Count(question => question.AnswerValue == AnswerNo);
        var unverifiedGapCount = unansweredCount;
        var highRiskGapCount = confirmedGapCount + unverifiedGapCount;

        if (applicableQuestions.Count == 0)
        {
            return new ReadinessScoreViewModel
            {
                Percentage = null,
                StatusLabel = "Not Started",
                StatusClass = "not-started",
                UnansweredCount = unansweredCount,
                HighRiskGapCount = highRiskGapCount,
                ConfirmedGapCount = confirmedGapCount,
                UnverifiedGapCount = unverifiedGapCount,
                ApplicableAnsweredCount = 0,
                NotApplicableCount = notApplicableCount,
                LastUpdatedBy = GetLastUpdatedBy(questionList),
                LastUpdatedAtUtc = GetLastUpdatedAt(questionList)
            };
        }

        var score = applicableQuestions.Sum(question => question.AnswerValue switch
        {
            AnswerYes => 1.0,
            AnswerPartial => 0.5,
            _ => 0.0
        });
        var percentage = (int)Math.Round(score * 100.0 / applicableQuestions.Count);

        return new ReadinessScoreViewModel
        {
            Percentage = percentage,
            StatusLabel = GetReadinessStatusLabel(percentage),
            StatusClass = GetReadinessStatusClass(percentage),
            UnansweredCount = unansweredCount,
            HighRiskGapCount = highRiskGapCount,
            ConfirmedGapCount = confirmedGapCount,
            UnverifiedGapCount = unverifiedGapCount,
            ApplicableAnsweredCount = applicableQuestions.Count,
            NotApplicableCount = notApplicableCount,
            LastUpdatedBy = GetLastUpdatedBy(questionList),
            LastUpdatedAtUtc = GetLastUpdatedAt(questionList)
        };
    }

    private static string? GetLastUpdatedBy(IEnumerable<ReadinessQuestionViewModel> questions)
    {
        return questions
            .Where(question => question.UpdatedAtUtc is not null)
            .OrderByDescending(question => question.UpdatedAtUtc)
            .Select(question => question.UpdatedByUserName)
            .FirstOrDefault(userName => !string.IsNullOrWhiteSpace(userName));
    }

    private static DateTimeOffset? GetLastUpdatedAt(IEnumerable<ReadinessQuestionViewModel> questions)
    {
        return questions
            .Where(question => question.UpdatedAtUtc is not null)
            .OrderByDescending(question => question.UpdatedAtUtc)
            .Select(question => question.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsAllowedAnswerValue(string? answerValue)
    {
        return answerValue is null || AnswerOptions.Contains(answerValue, StringComparer.Ordinal);
    }

    private static string? NormalizeAnswerValue(string? answerValue)
    {
        if (string.IsNullOrWhiteSpace(answerValue))
        {
            return null;
        }

        return AnswerOptions.FirstOrDefault(option => string.Equals(option, answerValue.Trim(), StringComparison.Ordinal));
    }

    private static string GetReadinessStatusLabel(int percentage)
    {
        return percentage switch
        {
            >= 80 => "Ready",
            >= 60 => "Improving",
            >= 40 => "Needs Improvement",
            _ => "High Risk"
        };
    }

    private static string GetReadinessStatusClass(int percentage)
    {
        return percentage switch
        {
            >= 80 => "ready",
            >= 60 => "improving",
            >= 40 => "needs-improvement",
            _ => "high-risk"
        };
    }
}
