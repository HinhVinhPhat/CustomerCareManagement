using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The care schedule and the care log. The schedule keeps the meaning <c>vw_CareSchedule</c> gives it
/// (latest care record of each active customer, appointment within seven days); the service only
/// groups the rows for the tab. A care record is added, never edited or deleted.
/// </summary>
public sealed class CareService(ApplicationDbContext db, ILogger<CareService> logger) : ICareService
{
    public async Task<IReadOnlyList<CareScheduleRow>> GetScheduleAsync(CancellationToken ct = default)
    {
        var rows = await db.VwCareSchedules
            .AsNoTracking()
            .OrderBy(v => v.NextAppointmentDate)
            .ThenBy(v => v.CustomerName)
            .Select(v => new CareScheduleRow(
                v.CareId,
                v.CustomerId,
                v.CustomerName,
                v.Phone,
                v.EmployeeId,
                v.FullName,
                v.CareType,
                v.Content,
                v.NextAppointmentDate,
                v.ScheduleStatus))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<CareSearchResult> SearchLogAsync(
        CareFilter filter, CancellationToken ct = default)
    {
        var query = db.Cares.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(c => c.Customer.CustomerName.Contains(q));
        }

        if (filter.Staff is { } staff)
        {
            query = query.Where(c => c.EmployeeId == staff);
        }

        if (filter.CareType is not null)
        {
            query = query.Where(c => c.CareType == filter.CareType);
        }

        if (filter.From is { } from)
        {
            query = query.Where(c => c.CareDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(c => c.CareDate <= to);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (CareFilter.SortByCustomer, false) => query
                .OrderBy(c => c.Customer.CustomerName).ThenByDescending(c => c.CareDate),
            (CareFilter.SortByCustomer, true) => query
                .OrderByDescending(c => c.Customer.CustomerName).ThenByDescending(c => c.CareDate),
            (CareFilter.SortByCareType, false) => query
                .OrderBy(c => c.CareType).ThenByDescending(c => c.CareDate),
            (CareFilter.SortByCareType, true) => query
                .OrderByDescending(c => c.CareType).ThenByDescending(c => c.CareDate),
            (_, false) => query.OrderBy(c => c.CareDate).ThenBy(c => c.CareId),
            _ => query.OrderByDescending(c => c.CareDate).ThenByDescending(c => c.CareId),
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new CareLogRow(
                c.CareId,
                c.CareDate,
                c.CustomerId,
                c.Customer.CustomerName,
                c.EmployeeId,
                c.Employee.FullName,
                c.CareType,
                c.Content,
                c.Result,
                c.SatisfactionScore,
                c.NextAppointmentDate))
            .ToListAsync(ct);

        return new CareSearchResult(rows, totalRows);
    }

    public async Task<int> CreateAsync(
        CareFormViewModel form, int employeeId, CancellationToken ct = default)
    {
        var customerId = form.CustomerId!.Value;
        await LookupQueries.EnsureActiveCustomerAsync(db, customerId, ct);

        var careDate = DateOnly.FromDateTime(DateTime.Today);

        if (!CareRules.AppointmentIsValid(careDate, form.NextAppointmentDate))
        {
            throw new BusinessException("Error_NextAppointmentBeforeCare");
        }

        var care = new Care
        {
            CustomerId = customerId,
            EmployeeId = employeeId,
            CareDate = careDate,
            CareType = form.CareType!,
            Content = Trimmed(form.Content),
            Result = Trimmed(form.Result),
            // A score belongs to a Survey only; the database repeats the rule, this keeps it out.
            SatisfactionScore = CareRules.NormaliseScore(form.CareType, form.SatisfactionScore),
            NextAppointmentDate = form.NextAppointmentDate
        };

        db.Cares.Add(care);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Recording a care visit failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }

        return care.CareId;
    }

    public async Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default) =>
        await LookupQueries.ActiveCustomersAsync(db, ct);

    public async Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default) =>
        await LookupQueries.StaffAsync(db, ct);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
