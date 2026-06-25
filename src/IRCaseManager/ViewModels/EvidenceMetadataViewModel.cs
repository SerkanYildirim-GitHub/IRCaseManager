using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.ViewModels;

public class EvidenceMetadataViewModel
{
    [Required, StringLength(80)]
    [Display(Name = "Evidence type")]
    public string EvidenceType { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 3)]
    [Display(Name = "Title or short label")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(256)]
    [Display(Name = "Source / reference")]
    public string Source { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Collected date/time")]
    public DateTimeOffset? CollectedAt { get; set; } = DateTimeOffset.Now;

    [StringLength(2000)]
    [Display(Name = "Analyst notes / comments")]
    public string? AnalystNotes { get; set; }

    public void Trim()
    {
        EvidenceType = EvidenceType?.Trim() ?? string.Empty;
        Name = Name?.Trim() ?? string.Empty;
        Description = Description?.Trim() ?? string.Empty;
        Source = Source?.Trim() ?? string.Empty;
        AnalystNotes = AnalystNotes?.Trim() ?? string.Empty;
    }
}
