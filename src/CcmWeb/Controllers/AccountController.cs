using System.Security.Claims;
using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CcmWeb.Controllers;

/// <summary>Sign in, sign out (design.md 6.1). The profile page belongs to M06.</summary>
public class AccountController(
    IAccountService accounts,
    ISettingService settings,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [EnableRateLimiting(WorkConstants.LoginRateLimitPolicy)]
    [HttpPost("/login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await accounts.AuthenticateAsync(model.Username, model.Password);

        if (outcome.Result != LoginResult.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Result == LoginResult.Locked
                ? "Error_AccountLocked"
                : "Error_InvalidCredentials");
            return View(model);
        }

        var user = outcome.User!;
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.AccountID.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(Utils.EmployeeIdClaim, user.EmployeeID.ToString())
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        logger.LogInformation("Employee {EmployeeID} signed in with role {Role}", user.EmployeeID, user.Role);

        await ApplyChosenSettingsAsync(user.EmployeeID);

        return RedirectToLocal(model.ReturnUrl);
    }

    /// <summary>Sign out is a POST, never a link, so it cannot be triggered by a crafted image tag.</summary>
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Only a local return URL is honoured, so the login cannot be used as an open redirect.</summary>
    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(HomeController.Index), "Home");

    /// <summary>
    /// The language and the theme follow the account, not the browser: right after signing in the
    /// two cookies are filled from <c>vw_EffectiveSettings</c>, so the next page is already
    /// rendered the way this employee chose. A new system default set by a Manager reaches
    /// everyone who never overrode it, because the view reports the default as their value.
    /// </summary>
    private async Task ApplyChosenSettingsAsync(int employeeID)
    {
        var language = await settings.GetEffectiveValueAsync(employeeID, WorkConstants.Settings.Language)
            ?? WorkConstants.DefaultLanguage;
        var theme = await settings.GetEffectiveValueAsync(employeeID, WorkConstants.Settings.Theme)
            ?? WorkConstants.DefaultTheme;

        Utils.SetCultureCookie(Response, language);
        Response.Cookies.Append(WorkConstants.ThemeCookieName, theme, Utils.PreferenceCookieOptions);
    }
}
