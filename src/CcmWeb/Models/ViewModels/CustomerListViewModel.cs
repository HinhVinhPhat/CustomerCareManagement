using System.Globalization;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of the customer list: the columns <c>sp_SearchCustomer</c> returns.</summary>
public sealed record CustomerListRow(
    int CustomerId,
    string CustomerName,
    string CustomerType,
    string? Email,
    string? Phone,
    string? Address,
    string Tier,
    DateOnly CreatedDate,
    int? OwnerEmployeeId,
    string? OwnerName,
    bool IsActive,
    decimal TotalValue);

/// <summary>An employee a customer can belong to: the owner filter and the form's owner dropdown.</summary>
public sealed record EmployeeOption(int EmployeeId, string FullName);

/// <summary>
/// The state of the customer list. Every value is read from the query string and written back into
/// the sort and pager links, so a filtered list is an address the user can copy into another tab
/// (design.md 3.4). The values are already sanitised when this object is built.
/// </summary>
public sealed class CustomerFilter
{
    /// <summary>Fields the list is sorted by. The same three names the procedure accepts.</summary>
    public const string SortByName = "Name";
    public const string SortByCreatedDate = "CreatedDate";
    public const string SortByTotalValue = "TotalValue";

    public static readonly string[] SortFields = [SortByName, SortByCreatedDate, SortByTotalValue];

    /// <summary>Keyword searched in the name, the e-mail and the phone; null = every customer.</summary>
    public string? Q { get; init; }

    /// <summary>A value of <c>CUSTOMER.Tier</c>; null = every tier.</summary>
    public string? Tier { get; init; }

    /// <summary>The owning employee; null = every owner.</summary>
    public int? Owner { get; init; }

    /// <summary>"Only my customers": resolved to the signed-in employee, never to a value from the form.</summary>
    public bool OnlyMine { get; init; }

    /// <summary>Include the deactivated customers (soft deleted, <c>IsActive = 0</c>).</summary>
    public bool IncludeInactive { get; init; }

    public string Sort { get; init; } = SortByName;
    public bool Desc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    /// <summary>True when the list is not simply "every active customer".</summary>
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Tier is not null || Owner is not null || OnlyMine || IncludeInactive;

    /// <summary>
    /// The same filter with one of the sort fields on top, and back on the first page. Clicking the
    /// field the list is already sorted by turns the direction around.
    /// </summary>
    public string SortQuery(string field) =>
        Build(field, Sort == field && !Desc, page: 1, PageSize);

    /// <summary>The same filter on another page.</summary>
    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    /// <summary>The same filter with another number of rows per page, back on the first page.</summary>
    public string PageSizeQuery(int size) => Build(Sort, Desc, page: 1, size);

    /// <summary>
    /// Builds the query string. Values that equal the default are left out, so a plain list keeps a
    /// plain address.
    /// </summary>
    private string Build(string sort, bool desc, int page, int pageSize)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            parameters.Add($"q={Uri.EscapeDataString(Q)}");
        }

        if (Tier is not null)
        {
            parameters.Add($"tier={Uri.EscapeDataString(Tier)}");
        }

        if (Owner is not null)
        {
            parameters.Add($"owner={Owner.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (OnlyMine)
        {
            // "true", not "1": the action binds these to bool, and "1" is not a bool the binder knows.
            parameters.Add("mine=true");
        }

        if (IncludeInactive)
        {
            parameters.Add("inactive=true");
        }

        if (sort != SortByName)
        {
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        if (desc)
        {
            parameters.Add("desc=true");
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

/// <summary>The customer list page: the filter, the rows of the current page and the pager facts.</summary>
public sealed class CustomerListViewModel
{
    public required CustomerFilter Filter { get; init; }

    public IReadOnlyList<CustomerListRow> Rows { get; init; } = [];

    public IReadOnlyList<EmployeeOption> Owners { get; init; } = [];

    /// <summary>Matching customers in total, as counted by the database before paging.</summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// Set when the database could not answer. The page shows the error instead of a list, because a
    /// failed query must never look like "no customers".
    /// </summary>
    public string? ErrorKey { get; init; }

    public int TotalPages => TotalRows == 0 ? 0 : (int)Math.Ceiling(TotalRows / (double)Filter.PageSize);

    public int FirstRowNumber => TotalRows == 0 ? 0 : ((Filter.Page - 1) * Filter.PageSize) + 1;

    public int LastRowNumber => Math.Min(Filter.Page * Filter.PageSize, TotalRows);

    public bool HasPreviousPage => Filter.Page > 1 && TotalPages > 0;

    public bool HasNextPage => Filter.Page < TotalPages;

    /// <summary>
    /// The page numbers the pager shows: a window of at most five around the current page, clamped to
    /// the ends, so a list with many pages still shows a short row of links.
    /// </summary>
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
