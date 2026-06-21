using System.ComponentModel.DataAnnotations;
using IRCaseManager.Models;

namespace IRCaseManager.ViewModels;

public class InvestigationDetailsViewModel : IValidatableObject
{
    [Display(Name = "Detection source")]
    public DetectionSource? DetectionSource { get; set; }

    [Display(Name = "Alert/report time")]
    public DateTimeOffset? AlertReportedAtUtc { get; set; }

    [StringLength(4000)]
    [Display(Name = "Affected user(s)")]
    public string? AffectedUsers { get; set; }

    [StringLength(4000)]
    [Display(Name = "Affected asset(s)")]
    public string? AffectedAssets { get; set; }

    [StringLength(4000)]
    [Display(Name = "Involved app(s) or tool(s)")]
    public string? InvolvedAppsOrTools { get; set; }

    [StringLength(4000)]
    [Display(Name = "Initial findings")]
    public string? InitialFindings { get; set; }

    [StringLength(4000)]
    [Display(Name = "Containment actions")]
    public string? ContainmentActions { get; set; }

    [StringLength(4000)]
    [Display(Name = "IOC summary")]
    public string? IocSummary { get; set; }

    [StringLength(4000)]
    [Display(Name = "Escalation reason")]
    public string? EscalationReason { get; set; }

    [StringLength(4000)]
    [Display(Name = "Closure summary")]
    public string? ClosureSummary { get; set; }

    public void Trim()
    {
        AffectedUsers = Normalize(AffectedUsers);
        AffectedAssets = Normalize(AffectedAssets);
        InvolvedAppsOrTools = Normalize(InvolvedAppsOrTools);
        InitialFindings = Normalize(InitialFindings);
        ContainmentActions = Normalize(ContainmentActions);
        IocSummary = Normalize(IocSummary);
        EscalationReason = Normalize(EscalationReason);
        ClosureSummary = Normalize(ClosureSummary);
    }

    public static InvestigationDetailsViewModel FromCase(Case irCase)
    {
        return new InvestigationDetailsViewModel
        {
            DetectionSource = irCase.DetectionSource,
            AlertReportedAtUtc = irCase.AlertReportedAtUtc,
            AffectedUsers = irCase.AffectedUsers,
            AffectedAssets = irCase.AffectedAssets,
            InvolvedAppsOrTools = irCase.InvolvedAppsOrTools,
            InitialFindings = irCase.InitialFindings,
            ContainmentActions = irCase.ContainmentActions,
            IocSummary = irCase.IocSummary,
            EscalationReason = irCase.EscalationReason,
            ClosureSummary = irCase.ClosureSummary
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DetectionSource is not null && !Enum.IsDefined(typeof(DetectionSource), DetectionSource.Value))
        {
            yield return new ValidationResult("Select a valid detection source.", [nameof(DetectionSource)]);
        }
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
