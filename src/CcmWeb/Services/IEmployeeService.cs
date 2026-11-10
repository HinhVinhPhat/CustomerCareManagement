using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the employee list plus the number of matches before paging.</summary>
public sealed record EmployeeSearchResult(IReadOnlyList<EmployeeRow> Rows, int TotalRows);

/// <summary>
/// Employees and departments (design.md 6.13). A person is never deleted: contracts, care, feedback
/// and the audit log point at them, so the account is locked instead. The database owns the rules -
/// one Manager per department is a unique index - and this service explains them before they fire.
/// </summary>
public interface IEmployeeService
{
    Task<EmployeeSearchResult> SearchAsync(EmployeeFilter filter, CancellationToken ct = default);

    Task<EmployeeFormViewModel?> GetAsync(int employeeId, CancellationToken ct = default);

    Task<int> CreateAsync(EmployeeFormViewModel form, CancellationToken ct = default);

    Task UpdateAsync(int employeeId, EmployeeFormViewModel form, CancellationToken ct = default);

    Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync(CancellationToken ct = default);

    /// <summary>Every employee, for the "who" dropdown of the audit filter.</summary>
    Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default);

    /// <summary>The department card: every department with its headcount and its head.</summary>
    Task<IReadOnlyList<DepartmentRow>> GetDepartmentRowsAsync(CancellationToken ct = default);

    Task<int> CreateDepartmentAsync(DepartmentFormModel form, CancellationToken ct = default);

    Task RenameDepartmentAsync(int departmentId, DepartmentFormModel form, CancellationToken ct = default);
}
