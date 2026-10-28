using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

public interface IContractService
{
    Task<ContractSearchResult> SearchAsync(ContractFilter filter, CancellationToken ct = default);
    Task<ContractDetailViewModel?> GetDetailAsync(int contractId, CancellationToken ct = default);
    Task<int> CreateAsync(ContractFormViewModel form, CancellationToken ct = default);
    Task SignAsync(int contractId, DateOnly? signDate, CancellationToken ct = default);
    Task RenewAsync(int contractId, DateOnly newEndDate, CancellationToken ct = default);
    Task CancelAsync(int contractId, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Models.ViewModels.EmployeeOption>> GetStaffAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductOption>> GetProductsAsync(CancellationToken ct = default);
}
