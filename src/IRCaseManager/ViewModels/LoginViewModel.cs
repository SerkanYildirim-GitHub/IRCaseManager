using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.ViewModels;

public class LoginViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(256)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember this browser")]
    public bool RememberMe { get; set; }
}
