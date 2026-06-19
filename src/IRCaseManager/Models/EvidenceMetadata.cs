using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class EvidenceMetadata
{
    public int Id { get; set; }

    public int CaseId { get; set; }

    public Case? Case { get; set; }

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(80)]
    public string EvidenceType { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(256)]
    public string Source { get; set; } = string.Empty;

    [StringLength(2000)]
    public string AnalystNotes { get; set; } = string.Empty;

    [StringLength(256)]
    public string StorageReference { get; set; } = string.Empty;

    [StringLength(128)]
    public string HashValue { get; set; } = string.Empty;

    [StringLength(80)]
    public string ChainOfCustodyReference { get; set; } = string.Empty;

    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;

    public int CollectedById { get; set; }
}
