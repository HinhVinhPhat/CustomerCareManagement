using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// Employees and departments. The list reads <c>EMPLOYEE</c> with its department and its account, so
/// the screen can say whether a person can sign in without a second query per row.
/// </summary>
public sealed class EmployeeService(ApplicationDbContext db, ILogger<EmployeeService> logger) : IEmployeeService
{
    public async Task<EmployeeSearchResult> SearchAsync(
        EmployeeFilter filter, CancellationToken ct = default)
    {
        var query = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(e => e.FullName.Contains(q)
                || e.Email.Contains(q)
                || e.Phone.Contains(q));
        }

        if (filter.Department is { } departmentId)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (filter.Position is not null)
        {
            query = query.Where(e => e.Position == filter.Position);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (EmployeeFilter.SortByDepartment, false) => query
                .OrderBy(e => e.Department.DepartmentName).ThenBy(e => e.FullName),
            (EmployeeFilter.SortByDepartment, true) => query
                .OrderByDescending(e => e.Department.DepartmentName).ThenBy(e => e.FullName),
            (_, true) => query.OrderByDescending(e => e.FullName),
            _ => query.OrderBy(e => e.FullName)
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => new EmployeeRow(
                e.EmployeeId,
                e.FullName,
                e.Email,
                e.Phone,
                e.Gender,
                e.Position,
                e.DepartmentId,
                e.Department.DepartmentName,
                e.Account != null))
            .ToListAsync(ct);

        return new EmployeeSearchResult(rows, totalRows);
    }

    public async Task<EmployeeFormViewModel?> GetAsync(int employeeId, CancellationToken ct = default)
    {
        var form = await db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .Select(e => new EmployeeFormViewModel
            {
                EmployeeId = e.EmployeeId,
                FullName = e.FullName,
                Email = e.Email,
                Phone = e.Phone,
                Gender = e.Gender,
                Position = e.Position,
                DepartmentId = e.DepartmentId
            })
            .FirstOrDefaultAsync(ct);

        if (form is not null)
        {
            form.Departments = await GetDepartmentsAsync(ct);
        }

        return form;
    }

    public async Task<int> CreateAsync(EmployeeFormViewModel form, CancellationToken ct = default)
    {
        await ValidateAsync(form, employeeId: null, ct);

        var employee = new Employee
        {
            FullName = form.FullName.Trim(),
            Email = form.Email.Trim(),
            Phone = form.Phone.Trim(),
            Gender = form.Gender!,
            Position = form.Position!,
            DepartmentId = form.DepartmentId!.Value
        };

        db.Employees.Add(employee);
        await SaveAsync(ct);

        return employee.EmployeeId;
    }

    public async Task UpdateAsync(
        int employeeId, EmployeeFormViewModel form, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct)
            ?? throw new BusinessException("Error_EmployeeNotFound");

        await ValidateAsync(form, employeeId, ct);

        employee.FullName = form.FullName.Trim();
        employee.Email = form.Email.Trim();
        employee.Phone = form.Phone.Trim();
        employee.Gender = form.Gender!;
        employee.Position = form.Position!;
        employee.DepartmentId = form.DepartmentId!.Value;

        await SaveAsync(ct);
    }

    public async Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync(CancellationToken ct = default) =>
        await db.Departments
            .AsNoTracking()
            .OrderBy(d => d.DepartmentName)
            .Select(d => new DepartmentOption(d.DepartmentId, d.DepartmentName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default) =>
        await LookupQueries.StaffAsync(db, ct);

    public async Task<IReadOnlyList<DepartmentRow>> GetDepartmentRowsAsync(CancellationToken ct = default)    {
        var departments = await db.Departments
            .AsNoTracking()
            .OrderBy(d => d.DepartmentName)
            .Select(d => new { d.DepartmentId, d.DepartmentName })
            .ToListAsync(ct);

        // The scaffold turned the "one Manager per department" unique index into a one-to-one
        // navigation, so the headcount comes from EMPLOYEE and is joined here.
        var people = await db.Employees
            .AsNoTracking()
            .GroupBy(e => e.DepartmentId)
            .Select(g => new
            {
                DepartmentId = g.Key,
                Count = g.Count(),
                Head = g.Where(e => e.Position == WorkConstants.Positions.Manager)
                        .Select(e => e.FullName)
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        var byDepartment = people.ToDictionary(p => p.DepartmentId);

        return departments
            .Select(d => byDepartment.TryGetValue(d.DepartmentId, out var group)
                ? new DepartmentRow(d.DepartmentId, d.DepartmentName, group.Count, group.Head)
                : new DepartmentRow(d.DepartmentId, d.DepartmentName, 0, null))
            .ToList();
    }

    public async Task<int> CreateDepartmentAsync(DepartmentFormModel form, CancellationToken ct = default)
    {
        var name = await EnsureDepartmentNameFreeAsync(form.DepartmentName, departmentId: null, ct);

        var department = new Department { DepartmentName = name };
        db.Departments.Add(department);
        await SaveAsync(ct);

        return department.DepartmentId;
    }

    public async Task RenameDepartmentAsync(
        int departmentId, DepartmentFormModel form, CancellationToken ct = default)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == departmentId, ct)
            ?? throw new BusinessException("Error_DepartmentNotFound");

        department.DepartmentName = await EnsureDepartmentNameFreeAsync(form.DepartmentName, departmentId, ct);
        await SaveAsync(ct);
    }

    /// <summary>
    /// The rules a person must satisfy before a write. They run here so the message sits next to the
    /// field; the CHECK constraints and the unique index remain the last word.
    /// </summary>
    private async Task ValidateAsync(EmployeeFormViewModel form, int? employeeId, CancellationToken ct)
    {
        if (!WorkConstants.EmployeeGenders.All.Contains(form.Gender ?? string.Empty))
        {
            throw new BusinessException("Error_EmployeeGenderInvalid");
        }

        if (!WorkConstants.Positions.All.Contains(form.Position ?? string.Empty))
        {
            throw new BusinessException("Error_EmployeePositionInvalid");
        }

        var phone = (form.Phone ?? string.Empty).Trim();
        if (phone.Length is < 9 or > 11 || !phone.All(char.IsAsciiDigit))
        {
            // The same rule as CHK_Employee_Phone, said in words the person can act on.
            throw new BusinessException("Error_EmployeePhoneInvalid");
        }

        var email = (form.Email ?? string.Empty).Trim();
        if (email.Length > 100 || !email.Contains('@') || !email.Contains('.'))
        {
            throw new BusinessException("Error_EmployeeEmailInvalid");
        }

        var departmentId = form.DepartmentId ?? 0;
        var departmentExists = await db.Departments
            .AsNoTracking()
            .AnyAsync(d => d.DepartmentId == departmentId, ct);

        if (!departmentExists)
        {
            throw new BusinessException("Error_DepartmentNotFound");
        }

        var emailTaken = await db.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Email == email && (employeeId == null || e.EmployeeId != employeeId), ct);

        if (emailTaken)
        {
            throw new BusinessException("Error_EmployeeEmailTaken");
        }

        var phoneTaken = await db.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Phone == phone && (employeeId == null || e.EmployeeId != employeeId), ct);

        if (phoneTaken)
        {
            throw new BusinessException("Error_EmployeePhoneTaken");
        }

        if (form.Position == WorkConstants.Positions.Manager)
        {
            // UX_Employee_DepartmentManager allows one Manager per department. Saying so here gives
            // the friendly sentence the design asks for instead of a unique-key error.
            var headExists = await db.Employees
                .AsNoTracking()
                .AnyAsync(e => e.DepartmentId == departmentId
                    && e.Position == WorkConstants.Positions.Manager
                    && (employeeId == null || e.EmployeeId != employeeId), ct);

            if (headExists)
            {
                throw new BusinessException("Error_DepartmentHasManager");
            }
        }
    }

    private async Task<string> EnsureDepartmentNameFreeAsync(
        string? name, int? departmentId, CancellationToken ct)
    {
        var clean = (name ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            throw new BusinessException("Error_Required");
        }

        var taken = await db.Departments
            .AsNoTracking()
            .AnyAsync(d => d.DepartmentName == clean
                && (departmentId == null || d.DepartmentId != departmentId), ct);

        if (taken)
        {
            throw new BusinessException("Error_DepartmentNameTaken");
        }

        return clean;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Saving an employee or a department failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }
}
