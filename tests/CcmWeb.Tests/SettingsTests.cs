using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Tests;

/// <summary>
/// The SETTINGS table on a real SQL Server: the constraints must refuse what the language says is
/// illegal, and the fallback rule must stay in the view instead of in application code.
/// </summary>
[Collection("database")]
public class SettingsTests
{
    // Employee 3 has no personal row in the sample data, so it is the one free to take an override.
    private const int EmployeeWithoutOverride = 3;

    [Fact]
    public async Task An_illegal_value_is_refused_by_the_database()
    {
        await using var db = CreateContext();

        db.Settings.Add(NewPersonalSetting(EmployeeWithoutOverride, "Language", "fr"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal("Error_SettingValueInvalid", Utils.TranslateSqlError(exception));
    }

    [Fact]
    public async Task An_unknown_key_is_refused_by_the_database()
    {
        await using var db = CreateContext();

        db.Settings.Add(NewPersonalSetting(EmployeeWithoutOverride, "PageSize", "20"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal("Error_UnknownSetting", Utils.TranslateSqlError(exception));
    }

    [Fact]
    public async Task An_employee_has_at_most_one_row_per_key()
    {
        await using var db = CreateContext();

        db.Settings.Add(NewPersonalSetting(EmployeeWithoutOverride, "Theme", "dark"));
        await db.SaveChangesAsync();

        db.Settings.Add(NewPersonalSetting(EmployeeWithoutOverride, "Theme", "light"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal("Error_SettingAlreadyExists", Utils.TranslateSqlError(exception));
    }

    [Fact]
    public async Task A_personal_choice_wins_over_the_system_default_and_the_default_applies_without_one()
    {
        await using var db = CreateContext();

        // Seeded: the system default for Language is "vi" for everyone.
        Assert.Equal("vi", await EffectiveValueAsync(db, 1, "Language"));

        db.Settings.Add(NewPersonalSetting(EmployeeWithoutOverride, "Language", "en"));
        await db.SaveChangesAsync();

        var personal = await db.VwEffectiveSettings
            .AsNoTracking()
            .SingleAsync(s => s.EmployeeId == EmployeeWithoutOverride && s.SettingKey == "Language");

        Assert.Equal("en", personal.SettingValue);
        Assert.Equal(1, personal.IsOverridden);

        var untouched = await db.VwEffectiveSettings
            .AsNoTracking()
            .SingleAsync(s => s.EmployeeId == 1 && s.SettingKey == "Language");

        Assert.Equal("vi", untouched.SettingValue);
        Assert.Equal(0, untouched.IsOverridden);

        // Leave the test database as it was found.
        db.Settings.RemoveRange(db.Settings.Where(s => s.EmployeeId == EmployeeWithoutOverride));
        await db.SaveChangesAsync();

        Assert.Equal("vi", await EffectiveValueAsync(db, EmployeeWithoutOverride, "Language"));
    }

    [Fact]
    public async Task Every_system_key_has_exactly_one_default_row()
    {
        await using var db = CreateContext();

        var defaults = await db.Settings
            .AsNoTracking()
            .Where(s => s.EmployeeId == null)
            .ToListAsync();

        Assert.Equal(WorkConstants.Settings.Keys.OrderBy(k => k), defaults.Select(s => s.SettingKey).OrderBy(k => k));
        Assert.All(defaults, row => Assert.True(WorkConstants.Settings.IsValidValue(row.SettingKey, row.SettingValue)));
    }

    private static async Task<string?> EffectiveValueAsync(ApplicationDbContext db, int employeeID, string key) =>
        (await db.VwEffectiveSettings
            .AsNoTracking()
            .SingleAsync(s => s.EmployeeId == employeeID && s.SettingKey == key))
        .SettingValue;

    private static Setting NewPersonalSetting(int employeeID, string key, string value) => new()
    {
        EmployeeId = employeeID,
        SettingKey = key,
        SettingValue = value,
        DataType = "String",
        Category = "Localization",
        UpdatedDate = DateTime.Now
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
