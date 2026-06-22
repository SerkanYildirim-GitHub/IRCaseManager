using System.ComponentModel.DataAnnotations;
using IRCaseManager.Models;

namespace IRCaseManager.ViewModels;

public class CreateCaseViewModel : IValidatableObject
{
    [Required]
    public CaseSeverity? Severity { get; set; }

    [Required]
    [Display(Name = "Case type")]
    public CaseType? CaseType { get; set; }

    [StringLength(100)]
    [Display(Name = "Queue")]
    public string AssignedTeam { get; set; } = string.Empty;

    [Display(Name = "Assigned to")]
    public int? AssignedUserId { get; set; }

    [Required, StringLength(4000, MinimumLength = 10)]
    [Display(Name = "Initial summary")]
    public string InitialSummary { get; set; } = string.Empty;

    public void Trim()
    {
        AssignedTeam = AssignedTeam?.Trim() ?? string.Empty;
        InitialSummary = InitialSummary?.Trim() ?? string.Empty;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Severity is not null && !Enum.IsDefined(typeof(CaseSeverity), Severity.Value))
        {
            yield return new ValidationResult("Select a valid severity.", [nameof(Severity)]);
        }

        if (CaseType is not null && !Enum.IsDefined(typeof(CaseType), CaseType.Value))
        {
            yield return new ValidationResult("Select a valid case type.", [nameof(CaseType)]);
        }

        var allowedQueues = new[] { "Incident Response", "IT Support", "Admin Review" };
        if (AssignedUserId is null && string.IsNullOrWhiteSpace(AssignedTeam))
        {
            yield return new ValidationResult("Select a queue.", [nameof(AssignedTeam)]);
        }

        if (!string.IsNullOrWhiteSpace(AssignedTeam) && !allowedQueues.Contains(AssignedTeam, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Select a valid queue.", [nameof(AssignedTeam)]);
        }
    }
}
