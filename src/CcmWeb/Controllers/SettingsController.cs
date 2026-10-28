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
    /// Saves every system-wide default in one go: the screen has a single "Lưu" button at the bottom,
    /// so the whole form is one transaction. Each key and value is checked here first, so an
    /// impossible pair never reaches the database (CHK_Settings_Value would refuse it anyway).
    /// A rejected value comes back on the same screen with what the Manager typed still in place.
    /// </summary>
    [HttpPost("/settings/system")]
    public async Task<IActionResult> SaveSystem(
        [FromForm] Dictionary<string, string> values, CancellationToken ct)
    {
        if (values.Count == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        foreach (var (key, value) in values)
        {
            if (!WorkConstants.Settings.IsKnownKey(key))
            {
                return await IndexWithAsync(values, "Error_UnknownSetting", ct);
            }

            if (!WorkConstants.Settings.IsValidValue(key, value))
            {
                return await IndexWithAsync(values, "Error_SettingValueInvalid", ct);
            }
        }

        try
        {
            await settings.SaveSystemValuesAsync(values, ct);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Saving the system settings failed.");
            return await IndexWithAsync(values, ex.MessageKey, ct);
        }

        logger.LogInformation("Employee {EmployeeID} saved {Count} system setting(s).",
            User.GetEmployeeID(), values.Count);

        // The shared banner in _Layout shows these two keys on every page.
        TempData["MessageKey"] = "Settings_Saved";
        TempData["MessageOk"] = true;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the screen again with the rejected values on it, so nothing the Manager typed is lost
    /// (AGENTS.md 5: a business error keeps the entered data).
    /// </summary>
    private async Task<IActionResult> IndexWithAsync(
        IReadOnlyDictionary<string, string> posted, string errorKey, CancellationToken ct)
    {
        var rows = await settings.GetSystemSettingsAsync(ct);

        return View("Index", new SettingsViewModel
        {
            SystemSettings = rows
                .Select(row => posted.TryGetValue(row.Key, out var value)
                    ? row with { Value = value }
                    : row)
                .ToList(),
            ErrorKey = errorKey
        });
    }
}
