using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public class Role
{
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string Name { get; set; } = string.Empty;

    [StringLength(256)]
    public string Description { get; set; } = string.Empty;

    public ICollection<ApplicationUser> Users { get; set; } = [];
}
