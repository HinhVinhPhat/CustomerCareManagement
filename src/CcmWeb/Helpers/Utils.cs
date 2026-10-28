using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Localization;

namespace CcmWeb.Helpers;

/// <summary>
/// A business rule reported by the database or by a Service. It carries a resource <b>key</b>
/// (never a finished sentence) plus optional arguments, so the controller can render it in the
/// language the user chose; see `ccm-language` section 6. <see cref="Exception.Message"/> is that
/// key and is meant for the log, not for the screen.
/// </summary>
public sealed class BusinessException(string messageKey, object?[]? arguments = null, Exception? inner = null)
    : Exception(messageKey, inner)
{
    public string MessageKey { get; } = messageKey;

    public object?[] Arguments { get; } = arguments ?? [];
}

/// <summary>Shared helpers: claims, SQL error translation, the legacy password hash, formatting, labels.</summary>
public static class Utils
{
    /// <summary>Claim holding <c>EMPLOYEE.EmployeeID</c>; the audit-log interceptor reads it.</summary>
    public const string EmployeeIdClaim = "employeeID";

    // ------------------------------------------------------------------ claims

    public static int? GetEmployeeID(this ClaimsPrincipal? user) =>
        int.TryParse(user?.FindFirst(EmployeeIdClaim)?.Value, out var id) ? id : null;

    public static int? GetAccountID(this ClaimsPrincipal? user) =>
        int.TryParse(user?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public static string GetDisplayName(this ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    public static string GetRole(this ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // ------------------------------------------------------------------ labels for coded values

    /// <summary>
    /// Turns a coded database value into the label the user reads, through the resource files:
    /// <c>localizer.Label("Theme", "dark")</c> finds the key <c>Theme_dark</c>. The code itself
    /// stays English in the database and in the code (`ccm-language` section 4).
    /// </summary>
    public static LocalizedString Label(this IStringLocalizer localizer, string group, string? code) =>
        localizer[$"{group}_{code}"];

    // ------------------------------------------------------------------ formatting
    // The display format follows the UI language the user picked, never the server's culture.

    /// <summary>Money: <c>80.000.000 VNĐ</c> in Vietnamese, <c>80,000,000 VND</c> in English.</summary>
    public static string FormatMoney(decimal value, CultureInfo? culture = null)
    {
        var vietnamese = (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName
            == WorkConstants.Settings.Languages.Vietnamese;

        return vietnamese
            ? value.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN").NumberFormat) + " VNĐ"
            : value.ToString("#,##0", CultureInfo.GetCultureInfo("en-US").NumberFormat) + " VND";
    }

    /// <summary>
    /// A satisfaction score (1-5) with one decimal place, in the number format of the language being
    /// read: <c>4,5</c> in Vietnamese and <c>4.5</c> in English.
    /// </summary>
    public static string FormatScore(double value) =>
        value.ToString("0.0", CultureInfo.CurrentUICulture);

    /// <summary>Dates keep <c>dd/MM/yyyy</c> in both languages, so a Vietnamese reader is never in doubt.</summary>
    public static string FormatDate(DateOnly? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string FormatDate(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string FormatDateTime(DateTime? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;

    // ------------------------------------------------------------------ display preferences

    /// <summary>
    /// The address to come back to after a language or theme switch: the current path and query with
    /// the culture parameters removed. A <c>?culture=</c> left in the address would win over the cookie
    /// the switch has just written (it is the first culture provider on purpose), so the user would
    /// click and see nothing change.
    /// </summary>
    public static string ReturnUrlWithoutCulture(HttpRequest request)
    {
        var kept = request.Query
            .Where(pair => pair.Key is not ("culture" or "ui-culture"))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value.ToString())}")
            .ToList();

        // Only a local address: the controller checks this again before redirecting.
        return kept.Count == 0 ? request.Path.Value ?? "/" : $"{request.Path}?{string.Join('&', kept)}";
    }

    /// <summary>
    /// The two display preferences (language, theme) live in cookies for a year. They are not
    /// secrets - the theme cookie is readable by site.js - and they carry no user data.
    /// </summary>
    public static CookieOptions PreferenceCookieOptions => new()
    {
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        HttpOnly = false
    };

    /// <summary>
    /// Writes the language into the framework's own culture cookie, so the next request is already
    /// rendered in it and CookieRequestCultureProvider reads back exactly what we wrote.
    /// </summary>
    public static void SetCultureCookie(HttpResponse response, string language) =>
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language)),
            PreferenceCookieOptions);

    // ------------------------------------------------------------------ list state from the query string
    // A list keeps its filters, its sort and its page in the URL, so the address can be copied into
    // another tab. Everything that comes in that way is clamped here: a hand-written ?page=abc must
    // still show a page, and ?pageSize=100000 must not ask the database for the whole table.

    /// <summary>Page number from the query string; anything below 1 becomes the first page.</summary>
    public static int SanitisePage(int? page) => page is null or < 1 ? 1 : page.Value;

    /// <summary>Rows per page from the query string, within the range the database accepts.</summary>
    public static int SanitisePageSize(int? size) =>
        size is >= WorkConstants.Settings.MinPageSize and <= WorkConstants.Settings.MaxPageSize
            ? size.Value
            : WorkConstants.Settings.DefaultPageSize;

    /// <summary>
    /// One of the fields a list may be sorted by; anything else (or nothing) becomes the default, so
    /// an unknown ?sort= never reaches the database as a column name.
    /// </summary>
    public static string SanitiseSort(string? sortBy, IReadOnlyList<string> allowed, string fallback) =>
        sortBy is not null && allowed.Contains(sortBy) ? sortBy : fallback;

    // ------------------------------------------------------------------ legacy demo password hash

    /// <summary>
    /// The sample data stores SHA2_256 of the password for the demo accounts: exactly 64 hex
    /// characters. A PasswordHasher hash is Base64 and about 84 characters, so the two forms
    /// cannot be confused.
    /// </summary>
    public static bool IsLegacySha256Hash(string? hash) =>
        hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    /// <summary>SHA-256 of the password as SQL Server sees it: a UTF-16 little-endian (nvarchar) string.</summary>
    public static string ComputeLegacySha256(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(password)));

    /// <summary>Compares the legacy hash case-insensitively in constant time.</summary>
    public static bool VerifyLegacySha256(string password, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(ComputeLegacySha256(password)),
            Encoding.ASCII.GetBytes(storedHash.ToUpperInvariant()));

    // ------------------------------------------------------------------ SQL error translation
    // Every known error becomes a resource KEY. The English text the database throws is never
    // shown to the user; the caller logs the original exception and shows the translated message.

    /// <summary>
    /// Walks the exception chain looking for a <see cref="SqlException"/> and returns the resource
    /// key of the message. Null means "not a known business error": log it and show a generic
    /// message.
    /// </summary>
    public static string? TranslateSqlError(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is SqlException sql)
            {
                return TranslateSqlNumber(sql.Number, sql.Message);
            }
        }

        return null;
    }

    /// <summary>The testable core of <see cref="TranslateSqlError"/>, free of the exception chain.</summary>
    public static string? TranslateSqlNumber(int number, string message) => number switch
    {
        >= 50001 and <= 50015 => ProcedureErrorKey(number),
        547 => TranslateConstraintErrorKey(message),
        2601 or 2627 => TranslateDuplicateErrorKey(message),
        _ => null
    };

    /// <summary>Message key for each <c>THROW</c> code of file 02 (see <c>ccm-database</c>).</summary>
    public static string ProcedureErrorKey(int number) => number switch
    {
        50001 => "Error_ContractHasNoLines",
        50002 => "Error_FeedbackNotFound",
        50003 => "Error_EmployeeNotFound",
        50004 => "Error_FeedbackNeedsAssignee",
        50005 => "Error_CustomerHasContracts",
        50006 => "Error_CustomerNotActive",
        50007 => "Error_EmployeeInvalid",
        50008 => "Error_ContractDetailInvalid",
        50009 => "Error_ProductDuplicated",
        50010 => "Error_ProductNotFound",
        50011 => "Error_QuantityInvalid",
        50012 => "Error_FeedbackStatusInvalid",
        50013 => "Error_ContractNotFound",
        50014 => "Error_ContractStatusForbids",
        50015 => "Error_DateInvalid",
        _ => "Error_Unexpected"
    };

    /// <summary>Message key for a CHECK / foreign-key violation (error 547), told apart by constraint name.</summary>
    public static string TranslateConstraintErrorKey(string message) =>
        message.Contains("CHK_Feedback_Assignee", StringComparison.Ordinal)
            ? "Error_FeedbackNeedsAssignee"
        : message.Contains("CHK_Customer_Business", StringComparison.Ordinal)
            ? "Error_BusinessCustomerNoGender"
        : message.Contains("CHK_Customer_Contact", StringComparison.Ordinal)
            ? "Error_CustomerNeedsContact"
        : message.Contains("CHK_Customer_Phone", StringComparison.Ordinal)
            ? "Error_CustomerPhoneInvalid"
        : message.Contains("CHK_Customer_Email", StringComparison.Ordinal)
            ? "Error_CustomerEmailInvalid"
        : message.Contains("CHK_Customer_DateOfBirth", StringComparison.Ordinal)
            ? "Error_CustomerBirthDateInvalid"
        : message.Contains("CHK_Care_SatisfactionScore", StringComparison.Ordinal)
            ? "Error_SurveyScoreOnly"
        : message.Contains("CHK_Care_NextAppointment", StringComparison.Ordinal)
            ? "Error_NextAppointmentBeforeCare"
        : message.Contains("CHK_Feedback_DueDate", StringComparison.Ordinal)
            ? "Error_FeedbackDueDateInvalid"
        : message.Contains("CHK_Settings_Key", StringComparison.Ordinal)
            ? "Error_UnknownSetting"
        : message.Contains("CHK_Settings_Value", StringComparison.Ordinal)
            ? "Error_SettingValueInvalid"
        : message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
          || message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase)
            ? "Error_RecordReferenced"
            : "Error_DatabaseRule";

    /// <summary>Message key for a unique violation (errors 2601 and 2627).</summary>
    public static string TranslateDuplicateErrorKey(string message) =>
        message.Contains("UX_Customer_Email", StringComparison.Ordinal)
            ? "Error_CustomerEmailTaken"
        : message.Contains("UQ_Settings_Scope", StringComparison.Ordinal)
            ? "Error_SettingAlreadyExists"
        : message.Contains("UX_Employee_DepartmentManager", StringComparison.Ordinal)
            ? "Error_DepartmentHasManager"
            : "Error_DuplicateData";
}
