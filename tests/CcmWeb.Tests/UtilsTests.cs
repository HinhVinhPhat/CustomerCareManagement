using System.Globalization;
using CcmWeb.Helpers;
using Microsoft.AspNetCore.Http;

namespace CcmWeb.Tests;

public class UtilsTests
{
    // ------------------------------------------------------------ SQL error translation
    // The user never reads the English text SQL Server produced: every known error becomes a
    // resource key, which the view renders in the language the user chose (ccm-language section 6).

    [Fact]
    public void A_business_message_from_the_database_is_translated_through_its_code() =>
        Assert.Equal(
            "Error_FeedbackNeedsAssignee",
            Utils.TranslateSqlNumber(50004, "The feedback must be assigned before its status is changed."));

    [Fact]
    public void Every_throw_code_of_the_database_has_its_own_message()
    {
        var keys = Enumerable.Range(50001, 15).Select(code => Utils.ProcedureErrorKey(code)).ToList();

        Assert.DoesNotContain("Error_Unexpected", keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void An_unknown_throw_code_falls_back_to_the_generic_message() =>
        Assert.Equal("Error_Unexpected", Utils.ProcedureErrorKey(50099));

    [Fact]
    public void A_check_constraint_violation_is_recognised_by_its_constraint_name() =>
        Assert.Equal(
            "Error_BusinessCustomerNoGender",
            Utils.TranslateSqlNumber(547,
                "The INSERT statement conflicted with the CHECK constraint \"CHK_Customer_Business\". " +
                "The conflict occurred in database \"CustomerCareDB\", table \"dbo.CUSTOMER\"."));

    [Fact]
    public void A_missing_contact_is_explained_through_its_constraint() =>
        Assert.Equal(
            "Error_CustomerNeedsContact",
            Utils.TranslateSqlNumber(547,
                "The INSERT statement conflicted with the CHECK constraint \"CHK_Customer_Contact\"."));

    [Fact]
    public void An_illegal_setting_value_is_recognised() =>
        Assert.Equal(
            "Error_SettingValueInvalid",
            Utils.TranslateSqlNumber(547,
                "The INSERT statement conflicted with the CHECK constraint \"CHK_Settings_Value\"."));

    [Fact]
    public void A_reference_constraint_violation_is_recognised() =>
        Assert.Equal(
            "Error_RecordReferenced",
            Utils.TranslateSqlNumber(547,
                "The DELETE statement conflicted with the REFERENCE constraint \"FK_CONTRACT_CUSTOMER\"."));

    [Fact]
    public void An_unrecognised_check_constraint_gets_the_generic_message() =>
        Assert.Equal(
            "Error_DatabaseRule",
            Utils.TranslateSqlNumber(547, "The INSERT statement conflicted with the CHECK constraint \"CHK_Other\"."));

    [Fact]
    public void A_duplicate_customer_email_gets_its_own_message() =>
        Assert.Equal(
            "Error_CustomerEmailTaken",
            Utils.TranslateSqlNumber(2601,
                "Cannot insert duplicate key row in object 'dbo.CUSTOMER' with unique index 'UX_Customer_Email'. " +
                "The duplicate key value is (test@customercare.com)."));

    [Fact]
    public void A_duplicate_setting_scope_gets_its_own_message() =>
        Assert.Equal(
            "Error_SettingAlreadyExists",
            Utils.TranslateSqlNumber(2627,
                "Violation of UNIQUE KEY constraint 'UQ_Settings_Scope'. " +
                "The duplicate key value is (<NULL>, Language)."));

    [Fact]
    public void A_duplicate_department_manager_gets_its_own_message() =>
        Assert.Equal(
            "Error_DepartmentHasManager",
            Utils.TranslateSqlNumber(2627,
                "Violation of UNIQUE KEY constraint 'UX_Employee_DepartmentManager'."));

    [Fact]
    public void An_unknown_error_is_never_shown_to_the_user() =>
        Assert.Null(Utils.TranslateSqlNumber(208, "Invalid object name 'dbo.Nope'."));

    // ------------------------------------------------------------ formatting

    [Fact]
    public void Money_is_written_in_the_language_being_read()
    {
        Assert.Equal("80.000.000 VNĐ", Utils.FormatMoney(80_000_000m, CultureInfo.GetCultureInfo("vi-VN")));
        Assert.Equal("80,000,000 VND", Utils.FormatMoney(80_000_000m, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void A_date_is_written_day_month_year_in_both_languages()
    {
        Assert.Equal("05/11/2026", Utils.FormatDate(new DateOnly(2026, 11, 5)));
        Assert.Equal("05/11/2026 14:30", Utils.FormatDateTime(new DateTime(2026, 11, 5, 14, 30, 0)));
    }

    [Fact]
    public void An_empty_optional_date_becomes_an_empty_string() =>
        Assert.Equal(string.Empty, Utils.FormatDate((DateOnly?)null));

    [Fact]
    public void A_business_exception_carries_a_key_and_arguments()
    {
        var exception = new BusinessException("Error_QuantityInvalid", ["abc"]);

        Assert.Equal("Error_QuantityInvalid", exception.MessageKey);
        Assert.Equal("abc", Assert.Single(exception.Arguments));
    }

    // ------------------------------------------------------------ coded values

    [Fact]
    public void Only_the_values_the_database_accepts_are_considered_valid()
    {
        Assert.True(WorkConstants.Settings.IsValidValue("Language", "vi"));
        Assert.True(WorkConstants.Settings.IsValidValue("Language", "en"));
        Assert.True(WorkConstants.Settings.IsValidValue("Theme", "dark"));
        Assert.False(WorkConstants.Settings.IsValidValue("Language", "fr"));
        Assert.False(WorkConstants.Settings.IsValidValue("Theme", "blue"));
        Assert.False(WorkConstants.Settings.IsValidValue("PageSize", "20"));
    }

    [Fact]
    public void Only_the_keys_the_database_accepts_are_known()
    {
        Assert.True(WorkConstants.Settings.IsKnownKey("Language"));
        Assert.True(WorkConstants.Settings.IsKnownKey("Theme"));
        Assert.False(WorkConstants.Settings.IsKnownKey("PageSize"));
    }

    // ------------------------------------------------------------ the return URL of a switch

    private static HttpRequest Request(string pathAndQuery)
    {
        var context = new DefaultHttpContext();
        var parts = pathAndQuery.Split('?', 2);
        context.Request.Path = parts[0];
        if (parts.Length == 2)
        {
            context.Request.QueryString = new QueryString($"?{parts[1]}");
        }

        return context.Request;
    }

    [Fact]
    public void A_culture_parameter_is_dropped_from_the_address_the_switch_comes_back_to() =>
        // Left in place it would beat the cookie the switch has just written, so the click would
        // look as if nothing happened.
        Assert.Equal("/login", Utils.ReturnUrlWithoutCulture(Request("/login?culture=en")));

    [Fact]
    public void Both_culture_parameters_are_dropped_and_every_other_parameter_survives() =>
        Assert.Equal(
            "/customers?page=2&q=abc",
            Utils.ReturnUrlWithoutCulture(Request("/customers?culture=en&page=2&ui-culture=en&q=abc")));

    [Fact]
    public void An_address_without_a_query_comes_back_unchanged()
    {
        Assert.Equal("/settings", Utils.ReturnUrlWithoutCulture(Request("/settings")));
        Assert.Equal("/", Utils.ReturnUrlWithoutCulture(Request("/")));
    }

    [Fact]
    public void A_parameter_value_is_escaped_again_so_the_address_stays_valid() =>
        Assert.Equal(
            "/customers?q=Nguy%E1%BB%85n%20V%C4%83n%20An",
            Utils.ReturnUrlWithoutCulture(Request("/customers?q=Nguy%E1%BB%85n%20V%C4%83n%20An")));
}
