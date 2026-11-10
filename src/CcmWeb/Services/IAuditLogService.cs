using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the audit log plus the number of matches before paging.</summary>
public sealed record AuditLogSearchResult(IReadOnlyList<AuditLogRow> Rows, int TotalRows);

/// <summary>
/// The system audit log (design.md 6.13): read-only, newest first. The rows are written by the
/// triggers of <c>database/02</c>, never by the application; this service only filters them.
/// </summary>
public interface IAuditLogService
{
    Task<AuditLogSearchResult> SearchAsync(AuditLogFilter filter, CancellationToken ct = default);
}
