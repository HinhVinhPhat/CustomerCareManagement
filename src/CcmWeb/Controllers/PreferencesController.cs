using CcmWeb.Helpers;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The two switches in the topbar. Each one writes the framework cookie (so the very next request
/// is rendered in the new language or theme) and stores the choice as the employee's own row in
/// SETTINGS (so the choice follows the account, not just this browser). Both are open to anonymous
/// visitors, because a display preference is harmless and the sign-in page offers the same switches;
/// a choice is only ever written to the database for a signed-in employee.
/// </summary>
[AllowAnonymous]
public class PreferencesController(ISettingService settings, ILogger<PreferencesController> logger) : Controller
{
    [HttpPost("/preferences/language")]
    public async Task<IActionResult> SetLanguage(string value, string? returnUrl, CancellationToken ct)
    {
        if (!WorkConstants.Settings.IsValidValue(WorkConstants.Settings.Language, value))
        {
            return Back(returnUrl);
        }

        // The framework's own cookie, so CookieRequestCultureProvider reads back what we wrote here.
        Utils.SetCultureCookie(Response, value);

        await RememberAsync(WorkConstants.Settings.Language, value, ct);

        return Back(returnUrl);
    }

    [HttpPost("/preferences/theme")]
    public async Task<IActionResult> SetTheme(string value, string? returnUrl, CancellationToken ct)
    {
        if (!WorkConstants.Settings.IsValidValue(WorkConstants.Settings.Theme, value))
        {
            return Back(returnUrl);
        }

        Response.Cookies.Append(WorkConstants.ThemeCookieName, value, Utils.PreferenceCookieOptions);

        await RememberAsync(WorkConstants.Settings.Theme, value, ct);

        return Back(returnUrl);
    }

    /// <summary>
    /// Stores the choice as the employee's own row. The cookie has already been set, so the user
    /// keeps the choice they just made even if the database write fails; a Manager can still see
    /// that the default was not overridden.
    /// </summary>
    private async Task RememberAsync(string key, string value, CancellationToken ct)
    {
        var employeeID = User.GetEmployeeID();
        if (employeeID is null)
        {
            return;
        }

        try
        {
            await settings.SaveUserValueAsync(employeeID.Value, key, value, ct);
            logger.LogInformation("Employee {EmployeeID} chose {Key} = {Value}.", employeeID, key, value);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Could not store {Key} = {Value} for employee {EmployeeID}.",
                key, value, employeeID);
        }
    }

    /// <summary>Returns to the page the switch was used on; anything but a local URL goes home.</summary>
    private IActionResult Back(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(HomeController.Index), "Home");
}
