using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>
/// Reads and writes the <c>SETTINGS</c> table. Values are always read through the views of file 02,
/// so the "a personal row beats the system default" rule lives in the database and is never
/// re-implemented here (see the `ccm-database` skill).
/// </summary>
public interface ISettingService
{
    /// <summary>The system-wide defaults, i.e. <c>vw_SystemSettings</c>.</summary>
    Task<IReadOnlyList<SystemSettingRow>> GetSystemSettingsAsync(CancellationToken ct = default);

    /// <summary>One system default; null when the key is unknown or the database cannot answer.</summary>
    Task<string?> GetSystemValueAsync(string key, CancellationToken ct = default);

    /// <summary>The value one employee really gets; null when that employee has no such setting.</summary>
    Task<string?> GetEffectiveValueAsync(int employeeID, string key, CancellationToken ct = default);

    /// <summary>
    /// The theme this request should render in: the cookie the user chose, otherwise the system
    /// default, otherwise <see cref="Helpers.WorkConstants.DefaultTheme"/>.
    /// </summary>
    Task<string> ResolveThemeAsync(CancellationToken ct = default);

    /// <summary>Stores the employee's own choice as a personal row.</summary>
    Task SaveUserValueAsync(int employeeID, string key, string value, CancellationToken ct = default);

    /// <summary>Stores the system-wide defaults (<c>EmployeeID IS NULL</c>) in one transaction; Manager only.</summary>
    Task SaveSystemValuesAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct = default);

    /// <summary>
    /// How many rows a list shows per page: the system default a Manager set, or the built-in
    /// <see cref="Helpers.WorkConstants.Settings.DefaultPageSize"/> when it is unset or unusable.
    /// A list may still ask for another size through its own URL.
    /// </summary>
    Task<int> GetPageSizeAsync(CancellationToken ct = default);
}
