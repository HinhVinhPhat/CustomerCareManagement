using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// Reads <c>AUDIT_LOG</c>. The filters are applied in SQL; the whole table is never loaded and then
/// filtered in memory. An unknown table or action is dropped by the controller, so a hand-written
/// address cannot hide real rows behind a filter nothing matches.
/// </summary>
public sealed class AuditLogService(ApplicationDbContext db) : IAuditLogService
{
    public async Task<AuditLogSearchResult> SearchAsync(
        AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsNoTracking();

        if (filter.Employee is { } employeeId)
        {
            query = query.Where(l => l.EmployeeId == employeeId);
        }

        if (filter.Table is not null)
        {
            query = query.Where(l => l.TableName == filter.Table);
        }

        if (filter.Action is not null)
        {
            query = query.Where(l => l.Action == filter.Action);
        }

        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(l => l.LogTime >= start);
        }

        if (filter.To is { } to)
        {
            // Half-open range: everything before the next midnight belongs to the "to" day.
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(l => l.LogTime < end);
        }

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(l => l.LogTime)
            .ThenByDescending(l => l.LogId)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(l => new AuditLogRow(
                l.LogId,
                l.LogTime,
                l.EmployeeId,
                l.Employee == null ? null : l.Employee.FullName,
                l.TableName,
                l.RecordId,
                l.Action,
                l.Detail))
            .ToListAsync(ct);

        return new AuditLogSearchResult(rows, totalRows);
    }
}
