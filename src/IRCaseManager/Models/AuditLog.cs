using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class AuditLog
{
    public int Id { get; set; }

    public int? ApplicationUserId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string EntityType { get; set; } = string.Empty;

    [StringLength(80)]
    public string EntityId { get; set; } = string.Empty;

    [StringLength(512)]
    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
