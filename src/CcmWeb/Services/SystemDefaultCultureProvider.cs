using CcmWeb.Helpers;
using Microsoft.AspNetCore.Localization;

namespace CcmWeb.Services;

/// <summary>
/// The last culture provider that can speak with authority: when neither the request (query string,
/// cookie) nor the browser asked for a language, the language a Manager set as the system default
/// applies. Returning null hands the decision to the next provider, so an unreachable database
/// still leaves the application in its hard-coded default of Vietnamese.
/// </summary>
public sealed class SystemDefaultCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // Resolved from the request, never injected: the service is scoped and the options object
        // that holds the providers is a singleton.
        var settings = httpContext.RequestServices.GetRequiredService<ISettingService>();
        var language = await settings.GetSystemValueAsync(WorkConstants.Settings.Language);

        return WorkConstants.Settings.IsValidValue(WorkConstants.Settings.Language, language)
            ? new ProviderCultureResult(language!)
            : null;
    }
}
