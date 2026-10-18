namespace CcmWeb.Helpers;

/// <summary>
/// Values shared with the database CHECK constraints and the role names.
/// They are added module by module: a list lives here only when a screen actually needs it.
/// </summary>
public static class WorkConstants
{
    public static class Roles
    {
        public const string Manager = "Manager";
        public const string Sales = "Sales";
        public const string Support = "Support";

        public static readonly string[] All = [Manager, Sales, Support];
    }

    /// <summary>Values of <c>vw_CareSchedule.ScheduleStatus</c> (design.md 2.2).</summary>
    public static class ScheduleStatus
    {
        public const string Overdue = "Overdue";
        public const string Today = "Today";
        public const string Upcoming = "Upcoming";
    }

    /// <summary>
    /// Keys and legal values of the <c>SETTINGS</c> table. They must stay in step with
    /// <c>CHK_Settings_Key</c> and <c>CHK_Settings_Value</c> in <c>database/01_CreateTables.sql</c>:
    /// the database is the authority, this list only lets the application validate before it writes.
    /// </summary>
    public static class Settings
    {
        public const string Language = "Language";
        public const string Theme = "Theme";

        public static readonly string[] Keys = [Language, Theme];

        public static class Languages
        {
            public const string Vietnamese = "vi";
            public const string English = "en";

            public static readonly string[] All = [Vietnamese, English];
        }

        public static class Themes
        {
            public const string Light = "light";
            public const string Dark = "dark";

            public static readonly string[] All = [Light, Dark];
        }

        /// <summary>Values <c>CHK_Settings_Value</c> accepts for a key; empty for an unknown key.</summary>
        public static string[] AllowedValues(string? key) => key switch
        {
            Language => Languages.All,
            Theme => Themes.All,
            _ => []
        };

        public static bool IsKnownKey(string? key) => Keys.Contains(key);

        public static bool IsValidValue(string? key, string? value) =>
            value is not null && AllowedValues(key).Contains(value);
    }

    /// <summary>Fallbacks used when neither the cookie, the account nor the database decides.</summary>
    public const string DefaultLanguage = Settings.Languages.Vietnamese;
    public const string DefaultTheme = Settings.Themes.Light;

    /// <summary>
    /// Cookie holding the chosen theme. The chosen language uses the framework's own culture cookie
    /// (<c>CookieRequestCultureProvider</c>), so the built-in provider reads back what we wrote.
    /// </summary>
    public const string ThemeCookieName = ".CcmWeb.Theme";

    /// <summary>Name of the rate-limiting policy applied to the login POST.</summary>
    public const string LoginRateLimitPolicy = "login";
}
