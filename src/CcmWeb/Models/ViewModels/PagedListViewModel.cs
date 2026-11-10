namespace CcmWeb.Models.ViewModels;

/// <summary>
/// Everything the shared pager (<c>Views/Shared/_Pager.cshtml</c>) needs. The four admin lists and the
/// audit log page their rows in SQL, exactly like the customer list does; keeping the shape in one
/// place means the pager is written once and no page builds its own links.
/// </summary>
public interface IPagedList
{
    int Page { get; }

    int PageSize { get; }

    int TotalRows { get; }

    /// <summary>Set when the database could not answer; the page shows the error instead of a list.</summary>
    string? ErrorKey { get; }

    /// <summary>The path of the list, e.g. <c>/admin/products</c>; the pager appends the query.</summary>
    string PageBaseUrl { get; }

    /// <summary>The same filters on another page.</summary>
    string PageQuery(int page);

    /// <summary>The same filters with another number of rows per page, back on the first page.</summary>
    string PageSizeQuery(int size);
}

/// <summary>The numbers the pager shows, worked out once for every list.</summary>
public static class Paging
{
    public static int TotalPageCount(this IPagedList list) =>
        list.TotalRows == 0 ? 0 : (int)Math.Ceiling(list.TotalRows / (double)list.PageSize);

    public static int FirstRowNumber(this IPagedList list) =>
        list.TotalRows == 0 ? 0 : ((list.Page - 1) * list.PageSize) + 1;

    public static int LastRowNumber(this IPagedList list) =>
        Math.Min(list.Page * list.PageSize, list.TotalRows);

    public static bool HasPreviousPage(this IPagedList list) =>
        list.Page > 1 && list.TotalPageCount() > 0;

    public static bool HasNextPage(this IPagedList list) => list.Page < list.TotalPageCount();

    /// <summary>
    /// A window of at most five page numbers around the current one, clamped to the ends, so a list
    /// with many pages still shows a short row of links.
    /// </summary>
    public static IReadOnlyList<int> PageNumbers(this IPagedList list)
    {
        var total = list.TotalPageCount();

        if (total <= 1)
        {
            return [];
        }

        var start = Math.Max(1, list.Page - 2);
        var end = Math.Min(total, start + 4);
        start = Math.Max(1, end - 4);

        return Enumerable.Range(start, end - start + 1).ToList();
    }
}

/// <summary>
/// The empty state of a list, told apart the way the customer list does it: a page past the end is
/// not "no data", and a filtered list with no match is not "nothing here yet". One partial renders
/// all three, so no page copies the block.
/// </summary>
public sealed record ListState(
    bool PastEnd,
    bool Filtered,
    string Icon,
    string EmptyTitleKey,
    string EmptyHintKey,
    string? FirstPageUrl = null,
    string? ClearUrl = null,
    string? AddUrl = null,
    string? AddLabelKey = null);
