namespace CcmWeb.Models.ViewModels;

/// <summary>One row of the system-default table on /settings (from <c>vw_SystemSettings</c>).</summary>
public sealed record SystemSettingRow(
    string Key,
    string Value,
    string Category,
    string? Description,
    DateTime UpdatedDate);

/// <summary>
/// The /settings page: the system-wide defaults a Manager can change. What every employee chose for
/// themselves is private to that employee and is deliberately not listed here - only
/// <c>vw_EffectiveSettings</c> answers it, for the employee the request belongs to.
/// </summary>
public sealed class SettingsViewModel
{
    public IReadOnlyList<SystemSettingRow> SystemSettings { get; init; } = [];
}
