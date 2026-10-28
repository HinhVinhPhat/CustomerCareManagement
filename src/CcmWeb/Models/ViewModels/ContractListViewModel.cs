using System.Globalization;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of the contract list: the columns <c>vw_ContractInfo</c> returns.</summary>
public sealed record ContractListRow(
    int ContractId,
    int CustomerId,
    string CustomerName,
    int EmployeeId,
    string EmployeeName,
    DateOnly SignDate,
    DateOnly EndDate,
    int? DaysRemaining,
    decimal TotalValue,
    string Status,
    string? Note);

/// <summary>One product line of a contract: <c>vw_ContractDetail</c>.</summary>
public sealed record ContractLineRow(
    int ProductId,
    string ProductName,
    string ProductType,
    int Quantity,
    decimal UnitPrice,
    decimal? LineTotal);

/// <summary>One entry of the audit log for a contract.</summary>
public sealed record ContractAuditRow(
    DateTime LogTime,
    int? EmployeeId,
    string? EmployeeName,
    string Action,
    string? Detail);

/// <summary>
/// The state of the contract list. Every value is read from the query string and written back into the
/// sort and pager links, so a filtered list is an address the user can copy into another tab.
/// </summary>
public sealed class ContractFilter
{
    public const string SortBySignDate = "SignDate";
    public const string SortByEndDate = "EndDate";
    public const string SortByTotalValue = "TotalValue";
    public const string SortByCustomerName = "CustomerName";

    public static readonly string[] SortFields =
        [SortBySignDate, SortByEndDate, SortByTotalValue, SortByCustomerName];

    public string? Q { get; init; }
    public string? Status { get; init; }
    public int? Staff { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool Expiring { get; init; }

    public string Sort { get; init; } = SortBySignDate;
    public bool Desc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Status is not null || Staff is not null
        || From is not null || To is not null || Expiring;

    public string SortQuery(string field) =>
        Build(field, Sort == field && !Desc, 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, 1, size);

    private string Build(string sort, bool desc, int page, int pageSize)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(Q))
            parameters.Add($"q={Uri.EscapeDataString(Q)}");
        if (Status is not null)
            parameters.Add($"status={Uri.EscapeDataString(Status)}");
        if (Staff is not null)
            parameters.Add($"staff={Staff.Value.ToString(CultureInfo.InvariantCulture)}");
        if (From is { } from)
            parameters.Add($"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (To is { } to)
            parameters.Add($"to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (Expiring)
            parameters.Add("expiring=true");
        if (sort != SortBySignDate)
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        if (desc)
            parameters.Add("desc=true");
        if (page > 1)
            parameters.Add($"page={page.ToString(CultureInfo.InvariantCulture)}");
        if (pageSize != WorkConstants.Settings.DefaultPageSize)
            parameters.Add($"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}");

        return parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
    }
}

/// <summary>The contract list page.</summary>
public sealed class ContractListViewModel
{
    public required ContractFilter Filter { get; init; }
    public IReadOnlyList<ContractListRow> Rows { get; init; } = [];
    public IReadOnlyList<EmployeeOption> Staff { get; init; } = [];
    public int TotalRows { get; init; }
    public string? ErrorKey { get; init; }

    public int TotalPages => TotalRows == 0 ? 0 : (int)Math.Ceiling(TotalRows / (double)Filter.PageSize);
    public int FirstRowNumber => TotalRows == 0 ? 0 : ((Filter.Page - 1) * Filter.PageSize) + 1;
    public int LastRowNumber => Math.Min(Filter.Page * Filter.PageSize, TotalRows);
    public bool HasPreviousPage => Filter.Page > 1 && TotalPages > 0;
    public bool HasNextPage => Filter.Page < TotalPages;
    public IReadOnlyList<int> PageNumbers
    {
        get
        {
            if (TotalPages <= 1) return [];
            var start = Math.Max(1, Filter.Page - 2);
            var end = Math.Min(TotalPages, start + 4);
            start = Math.Max(1, end - 4);
            return Enumerable.Range(start, end - start + 1).ToList();
        }
    }
}
