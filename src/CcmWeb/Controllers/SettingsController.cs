using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The settings screen. A Manager changes the system-wide defaults on <c>/settings</c>; every
/// employee's own language and theme are chosen from the topbar (see PreferencesController).
/// </summary>
[Authorize(Roles = WorkConstants.Roles.Manager)]
public class SettingsController(ISettingService settings, ILogger<SettingsController> logger) : Controller
{
    [HttpGet("/settings")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new SettingsViewModel
        {
            SystemSettings = await settings.GetSystemSettingsAsync(ct)
        };

        return View(model);
    }

    /// <summary>
    /// Saves one system-wide default. The key and the value are checked here first, so an
    /// impossible pair never reaches the database (CHK_Settings_Value would refuse it anyway).
    /// </summary>
    [HttpPost("/settings/system")]
    public async Task<IActionResult> SaveSystem(string settingKey, string settingValue, CancellationToken ct)
    {
        if (!WorkConstants.Settings.IsKnownKey(settingKey))
        {
            return BackToSettings("Error_UnknownSetting", ok: false);
        }

        if (!WorkConstants.Settings.IsValidValue(settingKey, settingValue))
        {
            return BackToSettings("Error_SettingValueInvalid", ok: false);
        }

        try
        {
            await settings.SaveSystemValueAsync(settingKey, settingValue, ct);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Saving the system setting {Key} failed.", settingKey);
            return BackToSettings(ex.MessageKey, ok: false);
        }

        logger.LogInformation("Employee {EmployeeID} set the system {Key} to {Value}.",
            User.GetEmployeeID(), settingKey, settingValue);

        return BackToSettings("Settings_Saved", ok: true);
    }

    private IActionResult BackToSettings(string messageKey, bool ok)
    {
        TempData["SettingsMessageKey"] = messageKey;
        TempData["SettingsMessageOk"] = ok;
        return RedirectToAction(nameof(Index));
    }
}
