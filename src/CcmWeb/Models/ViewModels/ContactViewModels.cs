using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of the contact list: the columns <c>CONTACT</c> carries.</summary>
public sealed record ContactRow(
    int ContactId,
    DateTime ContactTime,
    int CustomerId,
    string CustomerName,
    int EmployeeId,
    string EmployeeName,
    string Channel,
    string? Content,
    string? Result);

/// <summary>
/// The state of the contact list. Every value is read from the query string and written back into
/// the sort and pager links, so a filtered list is an address the user can copy into another tab
/// (design.md 3.4). The values are already sanitised when this object is built.
/// </summary>
public sealed class ContactFilter
{
    public const string SortByTime = "Time";
    public const string SortByCustomer = "Customer";
    public const string SortByStaff = "Staff";
    public const string SortByChannel = "Channel";

    public static readonly string[] SortFields =
        [SortByTime, SortByCustomer, SortByStaff, SortByChannel];

    /// <summary>Keyword searched in the customer name.</summary>
    public string? Q { get; init; }

    /// <summary>The employee who recorded the contact; null = every employee.</summary>
    public int? Staff { get; init; }

    /// <summary>A value of <c>CONTACT.Channel</c>; null = every channel.</summary>
    public string? Channel { get; init; }

    /// <summary>First contact-time day (inclusive); null = no lower bound.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Last contact-time day (inclusive); null = no upper bound.</summary>
    public DateOnly? To { get; init; }

    public string Sort { get; init; } = SortByTime;

    /// <summary>Newest first is the default, so the latest interaction is on top.</summary>
    public bool Desc { get; init; } = true;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Staff is not null || Channel is not null
        || From is not null || To is not null;

    /// <summary>One of the sort fields on top, back on the first page; the current field flips.</summary>
    public string SortQuery(string field) =>
        Build(field, Sort == field ? !Desc : field == SortByTime, 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, 1, size);

    private string Build(string sort, bool desc, int page, int pageSize)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            parameters.Add($"q={Uri.EscapeDataString(Q)}");
        }

        if (Staff is { } staff)
        {
            parameters.Add($"staff={staff.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Channel is not null)
        {
            parameters.Add($"channel={Uri.EscapeDataString(Channel)}");
        }

        if (From is { } from)
        {
            parameters.Add($"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        if (To is { } to)
        {
            parameters.Add($"to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        if (sort != SortByTime)
        {
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        if (!desc)
        {
            // The default is newest first, so only the ascending direction has to travel.
            parameters.Add("desc=false");
        }

        if (page > 1)
        {
            parameters.Add($"page={page.ToString(CultureInfo.InvariantCulture)}");
        }

        if (pageSize != WorkConstants.Settings.DefaultPageSize)
        {
            parameters.Add($"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        return parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
    }
}

/// <summary>The contact list page: the filter, the rows of the current page and the dialog form.</summary>
public sealed class ContactListViewModel
{
    public required ContactFilter Filter { get; init; }

    public IReadOnlyList<ContactRow> Rows { get; init; } = [];

    public IReadOnlyList<EmployeeOption> Staff { get; init; } = [];

    public IReadOnlyList<CustomerOption> Customers { get; init; } = [];

    /// <summary>Set when the database could not answer; the page shows the error instead of a list.</summary>
    public string? ErrorKey { get; init; }

    /// <summary>The dialog form. Kept here so a failed POST comes back with the values still in place.</summary>
    public ContactFormViewModel? Form { get; init; }

    /// <summary>True when the address asks for the dialog (the customer detail opens it pre-filled).</summary>
    public bool OpenDialog { get; init; }

    public int TotalRows { get; init; }

    public int TotalPages => TotalRows == 0 ? 0 : (int)Math.Ceiling(TotalRows / (double)Filter.PageSize);

    public int FirstRowNumber => TotalRows == 0 ? 0 : ((Filter.Page - 1) * Filter.PageSize) + 1;

    public int LastRowNumber => Math.Min(Filter.Page * Filter.PageSize, TotalRows);

    public bool HasPreviousPage => Filter.Page > 1 && TotalPages > 0;

    public bool HasNextPage => Filter.Page < TotalPages;

    public IReadOnlyList<int> PageNumbers
    {
        get
        {
            if (TotalPages <= 1)
            {
                return [];
            }

            var start = Math.Max(1, Filter.Page - 2);
            var end = Math.Min(TotalPages, start + 4);
            start = Math.Max(1, end - 4);

            return Enumerable.Range(start, end - start + 1).ToList();
        }
    }
}

/// <summary>
/// The "record contact" dialog (design.md 6.9). The staff member and the contact time are not fields:
/// the controller takes the employee from the signed-in claim and the service takes the clock.
/// </summary>
public sealed class ContactFormViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Validation_ContactCustomerRequired")]
    [Display(Name = "Common_Customer")]
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Validation_ContactChannelRequired")]
    [Display(Name = "Contact_Channel")]
    public string? Channel { get; set; }

    [StringLength(CareRules.MaxContentLength, ErrorMessage = "Validation_ContentTooLong")]
    [Display(Name = "Common_Content")]
    public string? Content { get; set; }

    [StringLength(CareRules.MaxResultLength, ErrorMessage = "Validation_ResultTooLong")]
    [Display(Name = "Common_Result")]
    public string? Result { get; set; }

    /// <summary>Where to go after a successful save; only a local address is accepted.</summary>
    public string? ReturnUrl { get; set; }

    public IReadOnlyList<CustomerOption> Customers { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Channel is not null && !WorkConstants.ContactChannels.All.Contains(Channel))
        {
            yield return new ValidationResult(
                "Validation_ContactChannelInvalid", [nameof(Channel)]);
        }
    }
}
