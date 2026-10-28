using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CcmWeb.Services;

public sealed class SettingsService(
    ApplicationDbContext db,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SettingsService> logger) : ISettingService
{
    // One instance lives per request and the layout asks for the theme on every render, so the
    // values are read at most once per request. Nothing is cached between requests, because a
    // Manager can change a default at any moment (and the cookie answers most requests anyway).
    private IReadOnlyList<SystemSettingRow>? systemSettings;
    private string? theme;

    public async Task<IReadOnlyList<SystemSettingRow>> GetSystemSettingsAsync(CancellationToken ct = default) =>
        systemSettings ??= await ReadSystemSettingsAsync(ct);

    public async Task<string?> GetSystemValueAsync(string key, CancellationToken ct = default)
    {
        var settings = await GetSystemSettingsAsync(ct);
        return settings.FirstOrDefault(row => row.Key == key)?.Value;
    }

    public async Task<string?> GetEffectiveValueAsync(int employeeID, string key, CancellationToken ct = default)
    {
        var row = await db.VwEffectiveSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeID && s.SettingKey == key, ct);

        return row?.SettingValue;
    }

    public async Task<string> ResolveThemeAsync(CancellationToken ct = default)
    {
        if (theme is not null)
        {
            return theme;
        }

        var chosen = httpContextAccessor.HttpContext?.Request.Cookies[WorkConstants.ThemeCookieName];

        if (!WorkConstants.Settings.IsValidValue(WorkConstants.Settings.Theme, chosen))
        {
            chosen = await GetSystemValueAsync(WorkConstants.Settings.Theme, ct);
        }

        return theme = WorkConstants.Settings.IsValidValue(WorkConstants.Settings.Theme, chosen)
            ? chosen!
            : WorkConstants.DefaultTheme;
    }

    public async Task SaveUserValueAsync(int employeeID, string key, string value, CancellationToken ct = default)
    {
        var row = await db.Settings
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeID && s.SettingKey == key, ct);

        if (row is null)
        {
            db.Settings.Add(new Setting
            {
                EmployeeId = employeeID,
                SettingKey = key,
                SettingValue = value,
                DataType = WorkConstants.Settings.DataTypeOf(key),
                Category = CategoryOf(key),
                UpdatedDate = DateTime.Now
            });
        }
        else
        {
            row.SettingValue = value;
            row.UpdatedDate = DateTime.Now;
        }

        await SaveAsync(ct);
    }

    public async Task SaveSystemValuesAsync(
        IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        if (values.Count == 0)
        {
            return;
        }

        // EmployeeID IS NULL is the system-wide row; UQ_Settings_Scope keeps it to one per key, so
        // this either updates that row or creates it the first time a Manager changes a default.
        // Every key is loaded and written in one SaveChanges, i.e. one transaction: the whole form
        // is applied or nothing is (the screen saves once, at the bottom).
        var rows = await db.Settings
            .Where(s => s.EmployeeId == null)
            .ToDictionaryAsync(s => s.SettingKey, ct);

        var now = DateTime.Now;

        foreach (var (key, value) in values)
        {
            if (rows.TryGetValue(key, out var row))
            {
                row.SettingValue = value;
                row.UpdatedDate = now;
            }
            else
            {
                db.Settings.Add(new Setting
                {
                    EmployeeId = null,
                    SettingKey = key,
                    SettingValue = value,
                    DataType = WorkConstants.Settings.DataTypeOf(key),
                    Category = CategoryOf(key),
                    UpdatedDate = now
                });
            }
        }

        await SaveAsync(ct);

        // The next request must see the new default, not the value read earlier in this one.
        systemSettings = null;
    }

    public async Task<int> GetPageSizeAsync(CancellationToken ct = default)
    {
        var stored = await GetSystemValueAsync(WorkConstants.Settings.PageSize, ct);

        // The CHECK constraint already keeps the value a whole number 1-100; a value that arrived
        // some other way must still not break a list, so it falls back instead of throwing.
        return WorkConstants.Settings.IsValidPageSize(stored)
            ? int.Parse(stored!, CultureInfo.InvariantCulture)
            : WorkConstants.Settings.DefaultPageSize;
    }

    /// <summary>
    /// A display preference must never stop a page from rendering: when the database cannot answer,
    /// the hard-coded defaults apply and the administrator gets a log entry.
    /// </summary>
    private async Task<IReadOnlyList<SystemSettingRow>> ReadSystemSettingsAsync(CancellationToken ct)
    {
        try
        {
            return await db.VwSystemSettings
                .AsNoTracking()
                .Select(s => new SystemSettingRow(
                    s.SettingKey, s.SettingValue, s.Category, s.Description, s.UpdatedDate))
                .ToListAsync(ct);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Could not read the system settings; the application defaults apply.");
            return [];
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // CHK_Settings_Value, UQ_Settings_Scope and a missing employee all surface here.
            // A resource key travels up, never the English text SQL Server produced.
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    /// <summary>Category of a key, so the settings screen can group them (matches CHK_Settings_Category).</summary>
    private static string CategoryOf(string key) => key switch
    {
        WorkConstants.Settings.Language => "Localization",
        WorkConstants.Settings.Theme => "Appearance",
        _ => "General"
    };
}
