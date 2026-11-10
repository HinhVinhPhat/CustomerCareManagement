using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The product catalogue. Filtering, sorting and paging run in SQL; the "already used" check is a
/// count of <c>CONTRACT_DETAIL</c> rows, because the foreign key would only tell us after a failed
/// delete and with an error nobody can read.
/// </summary>
public sealed class ProductCatalogService(ApplicationDbContext db, ILogger<ProductCatalogService> logger) : IProductService
{
    public async Task<ProductSearchResult> SearchAsync(
        ProductFilter filter, CancellationToken ct = default)
    {
        var query = db.ProductServices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(p => p.ProductName.Contains(q)
                || (p.Description != null && p.Description.Contains(q)));
        }

        if (filter.Type is not null)
        {
            query = query.Where(p => p.ProductType == filter.Type);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (ProductFilter.SortByPrice, false) => query
                .OrderBy(p => p.UnitPrice).ThenBy(p => p.ProductName),
            (ProductFilter.SortByPrice, true) => query
                .OrderByDescending(p => p.UnitPrice).ThenBy(p => p.ProductName),
            (_, true) => query.OrderByDescending(p => p.ProductName),
            _ => query.OrderBy(p => p.ProductName)
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new ProductRow(
                p.ProductId,
                p.ProductName,
                p.ProductType,
                p.UnitPrice,
                p.Description,
                p.ContractDetails.Count))
            .ToListAsync(ct);

        return new ProductSearchResult(rows, totalRows);
    }

    public async Task<ProductFormViewModel?> GetAsync(int productId, CancellationToken ct = default)
    {
        var product = await db.ProductServices
            .AsNoTracking()
            .Where(p => p.ProductId == productId)
            .Select(p => new ProductFormViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                ProductType = p.ProductType,
                UnitPrice = p.UnitPrice,
                Description = p.Description,
                UsageCount = p.ContractDetails.Count
            })
            .FirstOrDefaultAsync(ct);

        return product;
    }

    public async Task<int> CreateAsync(ProductFormViewModel form, CancellationToken ct = default)
    {
        var name = CleanName(form.ProductName);
        await EnsureNameFreeAsync(name, productId: null, ct);
        EnsureTypeValid(form.ProductType);

        var product = new ProductService
        {
            ProductName = name,
            ProductType = form.ProductType!,
            UnitPrice = form.UnitPrice,
            Description = Trimmed(form.Description)
        };

        db.ProductServices.Add(product);
        await SaveAsync(ct);

        return product.ProductId;
    }

    public async Task UpdateAsync(
        int productId, ProductFormViewModel form, CancellationToken ct = default)
    {
        var product = await db.ProductServices.FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            ?? throw new BusinessException("Error_ProductNotFound");

        var name = CleanName(form.ProductName);
        await EnsureNameFreeAsync(name, productId, ct);
        EnsureTypeValid(form.ProductType);

        // Only the catalogue changes. CONTRACT_DETAIL already holds the price each contract was sold
        // at, so an old contract keeps its value (trg_ContractDetail_UpdateTotal is not involved).
        product.ProductName = name;
        product.ProductType = form.ProductType!;
        product.UnitPrice = form.UnitPrice;
        product.Description = Trimmed(form.Description);

        await SaveAsync(ct);
    }

    public async Task DeleteAsync(int productId, CancellationToken ct = default)
    {
        var product = await db.ProductServices
            .Include(p => p.ContractDetails)
            .FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            ?? throw new BusinessException("Error_ProductNotFound");

        if (product.ContractDetails.Count > 0)
        {
            // The catalogue has no "hidden" state, so a used product stays and is only edited.
            throw new BusinessException("Error_ProductInUse");
        }

        db.ProductServices.Remove(product);
        await SaveAsync(ct);
    }

    /// <summary>The name is the natural key a Manager recognises, so it must stay unique.</summary>
    private async Task EnsureNameFreeAsync(string name, int? productId, CancellationToken ct)
    {
        var taken = await db.ProductServices
            .AsNoTracking()
            .AnyAsync(p => p.ProductName == name && (productId == null || p.ProductId != productId), ct);

        if (taken)
        {
            throw new BusinessException("Error_ProductNameTaken");
        }
    }

    private static void EnsureTypeValid(string? type)
    {
        if (!WorkConstants.ProductTypes.All.Contains(type ?? string.Empty))
        {
            throw new BusinessException("Error_ProductTypeInvalid");
        }
    }

    private static string CleanName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Saving a product failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }
}
