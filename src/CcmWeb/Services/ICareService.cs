using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the care log plus the number of matches before paging.</summary>
public sealed record CareSearchResult(IReadOnlyList<CareLogRow> Rows, int TotalRows);

/// <summary>
/// The care screens (design.md 6.10). The schedule is <c>vw_CareSchedule</c>; the log is the
/// <c>CARE</c> table. A care record is history: it is added, never edited or deleted. The employee
/// comes from the signed-in claim, the care date from the server clock.
/// </summary>
public interface ICareService
{
    Task<IReadOnlyList<CareScheduleRow>> GetScheduleAsync(CancellationToken ct = default);

    Task<CareSearchResult> SearchLogAsync(CareFilter filter, CancellationToken ct = default);

    Task<int> CreateAsync(CareFormViewModel form, int employeeId, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default);
}
