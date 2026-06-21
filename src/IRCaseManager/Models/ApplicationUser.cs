using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class ApplicationUser
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndUtc { get; set; }

    public DateTimeOffset? LastFailedLoginUtc { get; set; }

    public int RoleId { get; set; }

    public Role? Role { get; set; }

    public ICollection<CaseAssignment> CaseAssignments { get; set; } = [];
}
