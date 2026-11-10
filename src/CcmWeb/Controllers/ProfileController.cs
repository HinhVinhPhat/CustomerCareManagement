using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The employee's own page (design.md 6.14): read your information and change your password. The
/// employee comes from the signed-in claim, never from the form, so this screen can only ever touch
/// the account of the person using it.
/// </summary>
[Authorize]
public class ProfileController(
    IProfileService profiles,
    ILogger<ProfileController> logger) : Controller
{
    [HttpGet("/profile")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var info = await profiles.GetAsync(CurrentEmployeeId, ct);

        if (info is null)
        {
            // The account is signed in but its employee row is gone; the honest answer is 404.
            return NotFound();
        }

        return View(new ProfileViewModel { Info = info });
    }

    [HttpPost("/profile/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await IndexWithAsync(model, ct);
        }

        try
        {
            await profiles.ChangePasswordAsync(CurrentEmployeeId, model, ct);
            logger.LogInformation("Employee {EmployeeID} changed their own password.", CurrentEmployeeId);

            TempData["MessageKey"] = "Profile_PasswordChanged";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Changing the password of employee {EmployeeID} failed.", CurrentEmployeeId);
            AddBusinessError(ex.MessageKey);
            return await IndexWithAsync(model, ct);
        }
    }

    private int CurrentEmployeeId => User.GetEmployeeID() ?? 0;

    /// <summary>
    /// Shows the page again with the typed values (the passwords are not echoed back by the browser,
    /// but the field-level message stays where the user is looking).
    /// </summary>
    private async Task<IActionResult> IndexWithAsync(ChangePasswordViewModel form, CancellationToken ct)
    {
        var info = await profiles.GetAsync(CurrentEmployeeId, ct);

        if (info is null)
        {
            return NotFound();
        }

        return View(nameof(Index), new ProfileViewModel { Info = info, Password = form });
    }

    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_CurrentPasswordWrong" => nameof(ChangePasswordViewModel.CurrentPassword),
                "Error_PasswordTooShort" or "Error_PasswordSameAsOld"
                    => nameof(ChangePasswordViewModel.NewPassword),
                _ => string.Empty
            },
            messageKey);
}
