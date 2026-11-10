using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CcmWeb.Tests;

/// <summary>
/// The rules of the administration screens that live in code rather than in SQL: the password length
/// decision, the query string of a list link, the numbers the pager shows and the coded lists the
/// dropdowns are built from. No database is touched here.
/// </summary>
public class AdminRulesTests
{
    // ------------------------------------------------------------------ password rules

    [Theory]
    [InlineData("1234567", false)]     // one short of the minimum
    [InlineData("12345678", true)]     // exactly the minimum
    [InlineData("abcdefghij", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_password_is_accepted_from_eight_characters(string? password, bool expected) =>
        Assert.Equal(expected, PasswordRules.IsAcceptable(password));

    [Fact]
    public void A_password_longer_than_the_maximum_is_refused() =>
        Assert.False(PasswordRules.IsAcceptable(new string('a', WorkConstants.Accounts.PasswordMaxLength + 1)));

    [Fact]
    public void A_password_of_exactly_the_maximum_length_is_accepted() =>
        Assert.True(PasswordRules.IsAcceptable(new string('a', WorkConstants.Accounts.PasswordMaxLength)));

    // ------------------------------------------------------------------ list links

    [Fact]
    public void A_plain_list_has_a_plain_address()
    {
        var filter = new ProductFilter();

        Assert.Equal(string.Empty, filter.PageQuery(1));
        Assert.Equal(string.Empty, filter.PageSizeQuery(WorkConstants.Settings.DefaultPageSize));
    }

    [Fact]
    public void Clicking_the_column_already_sorted_turns_the_direction_around()
    {
        var filter = new ProductFilter { Q = "ERP" };

        // Already sorted by name ascending, so the same column now asks for descending.
        Assert.Equal("?q=ERP&desc=true", filter.SortQuery(ProductFilter.SortByName));
        Assert.Equal("?q=ERP&sort=Price", filter.SortQuery(ProductFilter.SortByPrice));
    }

    [Fact]
    public void A_filter_keeps_the_page_size_and_the_keyword()
    {
        var filter = new AccountFilter
        {
            Q = "mary smith",
            Role = WorkConstants.Roles.Sales,
            State = AccountFilter.StateLocked,
            Page = 2,
            PageSize = 50
        };

        var query = filter.PageQuery(3);

        Assert.Contains("q=mary%20smith", query);
        Assert.Contains("role=Sales", query);
        Assert.Contains("state=locked", query);
        Assert.Contains("page=3", query);
        Assert.Contains("pageSize=50", query);
    }

    [Fact]
    public void The_audit_filter_writes_the_two_days_as_iso_dates()
    {
        var filter = new AuditLogFilter
        {
            Table = "ACCOUNT",
            Action = "Update",
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 28)
        };

        var query = filter.PageQuery(1);

        Assert.Contains("table=ACCOUNT", query);
        Assert.Contains("action=Update", query);
        Assert.Contains("from=2026-10-01", query);
        Assert.Contains("to=2026-10-28", query);
    }

    [Fact]
    public void The_employee_filter_omits_a_default_department()
    {
        var filter = new EmployeeFilter { Department = null, Q = null };

        Assert.Equal(string.Empty, filter.PageQuery(1));
        Assert.False(filter.HasFilters);
    }

    // ------------------------------------------------------------------ pager numbers

    private sealed class FakeList(int page, int pageSize, int total) : IPagedList
    {
        public int Page { get; } = page;

        public int PageSize { get; } = pageSize;

        public int TotalRows { get; } = total;

        public string? ErrorKey => null;

        public string PageBaseUrl => "/admin/products";

        public string PageQuery(int pageNumber) => $"?page={pageNumber}";

        public string PageSizeQuery(int size) => $"?pageSize={size}";
    }

    [Fact]
    public void The_pager_counts_the_range_of_the_current_page()
    {
        var list = new FakeList(page: 2, pageSize: 10, total: 26);

        Assert.Equal(11, list.FirstRowNumber());
        Assert.Equal(20, list.LastRowNumber());
        Assert.Equal(3, list.TotalPageCount());
        Assert.True(list.HasPreviousPage());
        Assert.True(list.HasNextPage());
        Assert.Equal([1, 2, 3], list.PageNumbers());
    }

    [Fact]
    public void An_empty_list_shows_no_rows_and_no_pages()
    {
        var list = new FakeList(page: 1, pageSize: 10, total: 0);

        Assert.Equal(0, list.FirstRowNumber());
        Assert.Equal(0, list.LastRowNumber());
        Assert.Equal(0, list.TotalPageCount());
        Assert.False(list.HasPreviousPage());
        Assert.False(list.HasNextPage());
        Assert.Empty(list.PageNumbers());
    }

    [Fact]
    public void A_long_list_shows_a_short_window_of_page_numbers()
    {
        var numbers = new FakeList(page: 10, pageSize: 10, total: 300).PageNumbers();

        Assert.Equal([8, 9, 10, 11, 12], numbers);
    }

    // ------------------------------------------------------------------ coded lists

    [Fact]
    public void The_audit_filter_offers_every_action_the_triggers_write()
    {
        // The triggers of file 02 write these actions; a short list would hide real rows.
        Assert.Contains("Insert", WorkConstants.Audit.Actions);
        Assert.Contains("Update", WorkConstants.Audit.Actions);
        Assert.Contains("StatusChange", WorkConstants.Audit.Actions);
        Assert.Contains("Assign", WorkConstants.Audit.Actions);
        Assert.Contains("Delete", WorkConstants.Audit.Actions);
    }

    [Fact]
    public void The_audit_filter_offers_the_tables_that_carry_an_audit_trigger() =>
        Assert.Equal(
            ["CONTRACT", "FEEDBACK", "PRODUCT_SERVICE", "EMPLOYEE", "ACCOUNT"],
            WorkConstants.Audit.Tables);

    [Fact]
    public void The_position_list_matches_the_check_constraint()
    {
        Assert.Equal(["Manager", "Staff"], WorkConstants.Positions.All);
        Assert.Equal(["Male", "Female", "Other"], WorkConstants.EmployeeGenders.All);
        Assert.Equal(["Product", "Service"], WorkConstants.ProductTypes.All);
    }

    // ------------------------------------------------------------------ display-only fields

    [Theory]
    [InlineData(typeof(AccountEditViewModel), nameof(AccountEditViewModel.Username))]
    [InlineData(typeof(AccountEditViewModel), nameof(AccountEditViewModel.EmployeeName))]
    [InlineData(typeof(ResetPasswordViewModel), nameof(ResetPasswordViewModel.Username))]
    [InlineData(typeof(ResetPasswordViewModel), nameof(ResetPasswordViewModel.EmployeeName))]
    public void A_field_the_form_only_shows_is_never_validated(Type model, string property)
    {
        // B01: these two fields are drawn as plain text, so a posted form does not carry them.
        // Without [ValidateNever] the implicit "required" check of a non-nullable string made
        // ModelState invalid and the save was silently refused.
        var attribute = model.GetProperty(property)!
            .GetCustomAttributes(typeof(ValidateNeverAttribute), inherit: true);

        Assert.Single(attribute);
    }
}
