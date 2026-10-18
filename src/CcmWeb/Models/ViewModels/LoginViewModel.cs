using System.ComponentModel.DataAnnotations;

namespace CcmWeb.Models.ViewModels;

/// <summary>
/// The login form (design.md 6.1). The strings in the attributes are resource KEYS, not messages:
/// with AddDataAnnotationsLocalization the text the user reads is looked up in the language they
/// chose, so this file stays English while the screen is Vietnamese by default.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Validation_UsernameRequired")]
    [Display(Name = "Login_Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Login_Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
