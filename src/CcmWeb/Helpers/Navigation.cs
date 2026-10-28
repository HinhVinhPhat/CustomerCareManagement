namespace CcmWeb.Helpers;

/// <summary>One entry of the sidebar.</summary>
/// <param name="Key">Stable key used to highlight the current page.</param>
/// <param name="TitleKey">
/// Resource key of the label, never a finished title: the sidebar is rendered in the language the
/// user chose and the text lives in <c>Resources/SharedResource(.en).resx</c>.
/// </param>
/// <param name="Roles">Roles whose menu contains this entry (design.md 5.2).</param>
/// <param name="Built">False while the page belongs to a module that is not built yet.</param>
public sealed record NavItem(string Key, string TitleKey, string Icon, string Url, string[] Roles, bool Built);

/// <summary>
/// The sidebar menu, in the order of design.md section 5.2. "settings" is the one entry that did
/// not exist there: it holds the application defaults of M01b.
/// </summary>
public static class Navigation
{
    public static readonly IReadOnlyList<NavItem> Items =
    [
        new("dashboard", "Nav_Dashboard", "fa-gauge-high", "/", WorkConstants.Roles.All, true),
        new("customers", "Nav_Customers", "fa-users", "/customers", WorkConstants.Roles.All, true),
        new("contracts", "Nav_Contracts", "fa-file-contract", "/contracts", WorkConstants.Roles.All, true),
        new("contacts", "Nav_Contacts", "fa-phone", "/contacts", WorkConstants.Roles.All, true),
        new("care", "Nav_Care", "fa-heart", "/care", WorkConstants.Roles.All, true),
        new("feedback", "Nav_Feedback", "fa-comments", "/feedbacks",
            [WorkConstants.Roles.Manager, WorkConstants.Roles.Support], true),
        new("reports", "Nav_Reports", "fa-chart-column", "/reports", WorkConstants.Roles.All, true),
        new("administration", "Nav_Administration", "fa-gear", "/admin",
            [WorkConstants.Roles.Manager], false),
        new("settings", "Nav_Settings", "fa-sliders", "/settings",
            [WorkConstants.Roles.Manager], true)
    ];

    public static IEnumerable<NavItem> ForRole(string? role) =>
        Items.Where(item => role is not null && item.Roles.Contains(role));

    /// <summary>
    /// True when the menu entry points at the page being shown. A page inside a module keeps its
    /// entry highlighted (<c>/customers/5</c> is still the customers screen). The dashboard sits at
    /// "/" and therefore matches only itself.
    /// </summary>
    public static bool IsCurrent(NavItem item, string? currentPath)
    {
        var url = Normalise(item.Url);
        var path = Normalise(currentPath);

        return string.Equals(url, path, StringComparison.OrdinalIgnoreCase)
            || (url != "/" && path.StartsWith(url + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A path without its trailing slash; "/" stays "/".</summary>
    private static string Normalise(string? path)
    {
        var trimmed = (path ?? string.Empty).TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }
}
