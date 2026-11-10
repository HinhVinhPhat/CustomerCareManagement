using System.ComponentModel.DataAnnotations;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>What an employee may see about themselves: read from <c>EMPLOYEE</c> and <c>ACCOUNT</c>.</summary>
public sealed record ProfileInfo(
    string FullName,
    string Email,
    string Phone,
    string Gender,
    string Position,
    string DepartmentName,
    string Username,
    string Role);

/// <summary>The /profile page: the read-only card plus the change-password form.</summary>
public sealed class ProfileViewModel
{
    public required ProfileInfo Info { get; init; }

    public ChangePasswordViewModel Password { get; init; } = new();
}

/// <summary>The change-password form of /profile. The old password is required: this is the person's own account.</summary>
public sealed class ChangePasswordViewModel
{
    [Display(Name = "Password_Current")]
    [Required(ErrorMessage = "Error_Required")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Display(Name = "Password_New")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(WorkConstants.Accounts.PasswordMaxLength,
        MinimumLength = WorkConstants.Accounts.PasswordMinLength, ErrorMessage = "Error_PasswordTooShort")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Password_Confirm")]
    [Required(ErrorMessage = "Error_Required")]
    [Compare(nameof(NewPassword), ErrorMessage = "Error_PasswordMismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
