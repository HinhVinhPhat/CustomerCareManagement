using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the customer list plus the total number of matches.</summary>
public sealed record CustomerSearchResult(IReadOnlyList<CustomerListRow> Rows, int TotalRows);

/// <summary>
/// Everything the customer screens do with the database. Filtering, sorting and paging happen inside
/// <c>sp_SearchCustomer</c>; the status change goes through <c>sp_SetCustomerStatus</c>; a customer is
/// never deleted (AGENTS.md 4). The detail tabs read the views of file 02.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// One page of customers. <paramref name="ownerEmployeeId"/> is the resolved owner ("only my
    /// customers" passes the employee from the claim); null means every owner.
    /// </summary>
    Task<CustomerSearchResult> SearchAsync(
        CustomerFilter filter, int? ownerEmployeeId, CancellationToken ct = default);

    /// <summary>The whole detail page; null when the customer does not exist.</summary>
    Task<CustomerDetailViewModel?> GetDetailAsync(int customerId, CancellationToken ct = default);

    /// <summary>The form filled in with the stored values; null when the customer does not exist.</summary>
    Task<CustomerFormViewModel?> GetForEditAsync(int customerId, CancellationToken ct = default);

    /// <summary>Creates the customer and returns its new id.</summary>
    Task<int> CreateAsync(CustomerFormViewModel form, CancellationToken ct = default);

    /// <summary>Saves the form over an existing customer.</summary>
    Task UpdateAsync(int customerId, CustomerFormViewModel form, CancellationToken ct = default);

    /// <summary>Deactivates or reactivates through <c>sp_SetCustomerStatus</c>; never a delete.</summary>
    Task SetStatusAsync(int customerId, bool isActive, CancellationToken ct = default);

    /// <summary>The employees the owner filter and the owner dropdown offer.</summary>
    Task<IReadOnlyList<EmployeeOption>> GetOwnersAsync(CancellationToken ct = default);

    /// <summary>
    /// Gợi ý cho ô tìm nhanh trên thanh trên cùng: tối đa <paramref name="count"/> khách hàng đang
    /// hoạt động, chỉ mã, tên và loại. Từ khóa ngắn hơn 2 ký tự trả về danh sách rỗng mà không hỏi
    /// cơ sở dữ liệu.
    /// </summary>
    Task<IReadOnlyList<CustomerSuggestion>> SuggestAsync(
        string? keyword, int count, CancellationToken ct = default);
}
