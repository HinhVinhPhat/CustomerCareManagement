using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>
/// The employee's own page (design.md 6.14): read your information, change your password. The
/// employee id always comes from the signed-in claim, so one person can never look at - or change -
/// another person's account through this screen.
/// </summary>
public interface IProfileService
{
    /// <summary>The signed-in employee, or null when the row no longer exists.</summary>
    Task<ProfileInfo?> GetAsync(int employeeId, CancellationToken ct = default);

    Task ChangePasswordAsync(int employeeId, ChangePasswordViewModel form, CancellationToken ct = default);
}
