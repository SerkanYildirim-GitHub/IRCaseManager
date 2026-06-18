using System.ComponentModel.DataAnnotations;
using IRCaseManager.Models;

namespace IRCaseManager.ViewModels;

public class CreateCaseViewModel : IValidatableObject
{
    [Required, StringLength(160, MinimumLength = 4)]
    [Display(Name = "Case title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    public CaseSeverity? Severity { get; set; }

    [Required]
    [Display(Name = "Case type")]
    public CaseType? CaseType { get; set; }

    [StringLength(80)]
    [Display(Name = "Source reference")]
    public string? SourceReference { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Assigned team")]
    public string AssignedTeam { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 10)]
    [Display(Name = "Initial summary")]
    public string InitialSummary { get; set; } = string.Empty;

    public void Trim()
    {
        Title = Title.Trim();
        SourceReference = string.IsNullOrWhiteSpace(SourceReference) ? null : SourceReference.Trim();
        AssignedTeam = AssignedTeam.Trim();
        InitialSummary = InitialSummary.Trim();
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
    }
}
