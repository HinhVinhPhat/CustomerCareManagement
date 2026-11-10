using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the product list plus the number of matches before paging.</summary>
public sealed record ProductSearchResult(IReadOnlyList<ProductRow> Rows, int TotalRows);

/// <summary>
/// The product catalogue (design.md 6.13). Products are read by every role and changed by a Manager;
/// `CONTRACT_DETAIL` keeps the price it was sold at, so editing a price never rewrites history.
/// </summary>
public interface IProductService
{
    Task<ProductSearchResult> SearchAsync(ProductFilter filter, CancellationToken ct = default);

    /// <summary>The form of an existing product, or null when the id does not exist.</summary>
    Task<ProductFormViewModel?> GetAsync(int productId, CancellationToken ct = default);

    Task<int> CreateAsync(ProductFormViewModel form, CancellationToken ct = default);

    Task UpdateAsync(int productId, ProductFormViewModel form, CancellationToken ct = default);

    /// <summary>Deletes a product that no contract line uses; a used product is refused.</summary>
    Task DeleteAsync(int productId, CancellationToken ct = default);
}
