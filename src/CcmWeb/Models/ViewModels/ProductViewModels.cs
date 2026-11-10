using System.ComponentModel.DataAnnotations;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of <c>PRODUCT_SERVICE</c>, with how many contract lines already use it.</summary>
public sealed record ProductRow(
    int ProductId,
    string ProductName,
    string ProductType,
    decimal UnitPrice,
    string? Description,
    int UsageCount);

/// <summary>The state of the product list; every value is written back into the pager and sort links.</summary>
public sealed class ProductFilter
{
    public const string SortByName = "Name";
    public const string SortByPrice = "Price";

    public static readonly string[] SortFields = [SortByName, SortByPrice];

    /// <summary>Keyword searched in the name and the description; null = every product.</summary>
    public string? Q { get; init; }

    /// <summary>A value of <c>PRODUCT_SERVICE.ProductType</c>; null = both types.</summary>
    public string? Type { get; init; }

    public string Sort { get; init; } = SortByName;
    public bool Desc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters => !string.IsNullOrWhiteSpace(Q) || Type is not null;

    public string SortQuery(string field) =>
        Build(field, Sort == field && !Desc, page: 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, page: 1, size);

    private string Build(string sort, bool desc, int page, int pageSize) =>
        new ListQuery()
            .Add("q", Q)
            .Add("type", Type)
            .Add("sort", sort, SortByName)
            .AddFlag("desc", desc)
            .AddNumber("page", page, 1)
            .AddNumber("pageSize", pageSize, WorkConstants.Settings.DefaultPageSize)
            .ToString();
}

/// <summary>The product list page: the filter, the rows of the current page and the pager facts.</summary>
public sealed class ProductListViewModel : IPagedList
{
    public required ProductFilter Filter { get; init; }

    public IReadOnlyList<ProductRow> Rows { get; init; } = [];

    public int TotalRows { get; init; }

    public string? ErrorKey { get; init; }

    /// <summary>Only a Manager may add, edit or delete; Sales and Support read the catalogue.</summary>
    public bool CanEdit { get; init; }

    public int Page => Filter.Page;

    public int PageSize => Filter.PageSize;

    public string PageBaseUrl => "/admin/products";

    public string PageQuery(int page) => Filter.PageQuery(page);

    public string PageSizeQuery(int size) => Filter.PageSizeQuery(size);

    public ListState EmptyState => new(
        PastEnd: Filter.Page > 1,
        Filtered: Filter.HasFilters,
        Icon: "fa-box-open",
        EmptyTitleKey: "Product_NoneYet",
        EmptyHintKey: "Product_NoneYetHint",
        FirstPageUrl: $"{PageBaseUrl}{Filter.PageQuery(1)}",
        ClearUrl: PageBaseUrl,
        AddUrl: CanEdit ? "/admin/products/create" : null,
        AddLabelKey: "Product_Add");
}

/// <summary>
/// The add / edit form. <see cref="ProductId"/> is null when adding; the route carries the id when
/// editing, so a POST can never move the change onto another product.
/// </summary>
public sealed class ProductFormViewModel
{
    public int? ProductId { get; init; }

    public bool IsEdit => ProductId is not null;

    [Display(Name = "Product_Name")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(150, ErrorMessage = "Error_TooLong")]
    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "Product_Type")]
    [Required(ErrorMessage = "Error_Required")]
    public string? ProductType { get; set; }

    [Display(Name = "Product_UnitPrice")]
    [Range(0, 999_999_999_999_999.99, ErrorMessage = "Error_ProductPriceInvalid")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Product_Description")]
    [StringLength(500, ErrorMessage = "Error_TooLong")]
    public string? Description { get; set; }

    /// <summary>How many contract lines already use the product; blocks the delete button.</summary>
    public int UsageCount { get; init; }
}
