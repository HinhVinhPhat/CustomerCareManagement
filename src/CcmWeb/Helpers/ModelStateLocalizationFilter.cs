using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;

namespace CcmWeb.Helpers;

/// <summary>
/// Controllers report a business error by putting the resource <b>key</b> into <c>ModelState</c>
/// ("Error_CustomerEmailTaken"), the same way the view models do through their attributes.
/// <c>AddDataAnnotationsLocalization</c> only translates the attribute messages, so without this
/// filter the key itself reaches the screen - which is why the sign-in page used to show
/// "Error_InvalidCredentials".
///
/// The filter runs after the action and before the view renders, and replaces every message that is
/// a known resource key with the text in the language the user is reading. A message that is not a
/// key (already a sentence, or the text of an unexpected exception) is left exactly as it is.
/// </summary>
public sealed class ModelStateLocalizationFilter(IStringLocalizer<SharedResource> localizer) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        // A redirect carries no ModelState to render; only a view does.
        if (context.Result is not ViewResult)
        {
            return;
        }

        foreach (var key in context.ModelState.Keys.ToList())
        {
            var entry = context.ModelState[key];

            if (entry is null || entry.Errors.Count == 0)
            {
                continue;
            }

            var messages = new List<string>(entry.Errors.Count);
            var translated = false;

            foreach (var error in entry.Errors)
            {
                var message = error.ErrorMessage;
                var localized = message.Length == 0 ? default : localizer[message];

                if (localized is { ResourceNotFound: false })
                {
                    translated = true;
                    messages.Add(localized.Value);
                }
                else
                {
                    // An exception message or a sentence somebody typed: shown as it is.
                    messages.Add(message);
                }
            }

            if (!translated)
            {
                continue;
            }

            // Rebuilding the entry keeps the attempted value, which is what re-displays what the
            // user typed in the form after a failed save.
            var rawValue = entry.RawValue;
            var attemptedValue = entry.AttemptedValue;

            context.ModelState.Remove(key);
            context.ModelState.SetModelValue(key, rawValue, attemptedValue);

            foreach (var message in messages)
            {
                context.ModelState.AddModelError(key, message);
            }
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // Nothing to undo: the translation only touches what this response renders.
    }
}
