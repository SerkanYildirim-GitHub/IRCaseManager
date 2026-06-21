using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.ViewModels;

public class TimelineEntryViewModel : IValidatableObject
{
    private static readonly string[] AllowedActivityTypes =
    [
        "Note",
        "Investigation",
        "Containment",
        "Communication",
        "Decision",
        "Other"
    ];

    [Required]
    [Display(Name = "Activity type")]
    public string ActivityType { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 3)]
    [Display(Name = "Activity text")]
    public string Body { get; set; } = string.Empty;

    public static IReadOnlyList<string> ActivityTypes => AllowedActivityTypes;

    public void Trim()
    {
        ActivityType = ActivityType?.Trim() ?? string.Empty;
        Body = Body?.Trim() ?? string.Empty;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ActivityType) &&
            !AllowedActivityTypes.Contains(ActivityType, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Select a valid activity type.", [nameof(ActivityType)]);
        }
    }
}
