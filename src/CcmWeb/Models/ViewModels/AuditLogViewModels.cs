using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of <c>AUDIT_LOG</c>. <c>Detail</c> is written by the triggers as codes, not as a sentence.</summary>
public sealed record AuditLogRow(
    long LogId,
    DateTime LogTime,
    int? EmployeeId,
    string? EmployeeName,
    string TableName,
    int RecordId,
    string Action,
    string? Detail);

/// <summary>The state of the audit screen: who, which table, which action and which days.</summary>
public sealed class AuditLogFilter
{
    public int? Employee { get; init; }

    /// <summary>A value of <see cref="WorkConstants.Audit.Tables"/>; null = every table.</summary>
    public string? Table { get; init; }

    /// <summary>A value of <see cref="WorkConstants.Audit.Actions"/>; null = every action.</summary>
    public string? Action { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        Employee is not null || Table is not null || Action is not null || From is not null || To is not null;

    public string PageQuery(int page) => Build(page, PageSize);

    public string PageSizeQuery(int size) => Build(page: 1, size);

    private string Build(int page, int pageSize) =>
        new ListQuery()
            .AddNumber("employee", Employee ?? 0, 0)
            .Add("table", Table)
            .Add("action", Action)
            .Add("from", From?.ToString("yyyy-MM-dd"))
            .Add("to", To?.ToString("yyyy-MM-dd"))
            .AddNumber("page", page, 1)
            .AddNumber("pageSize", pageSize, WorkConstants.Settings.DefaultPageSize)
            .ToString();
}

/// <summary>The audit page: read-only rows, newest first.</summary>
public sealed class AuditLogListViewModel : IPagedList
{
    public required AuditLogFilter Filter { get; init; }

    public IReadOnlyList<AuditLogRow> Rows { get; init; } = [];

    public int TotalRows { get; init; }

    public string? ErrorKey { get; init; }

    /// <summary>The employees of the "who" dropdown, for the filter only.</summary>
    public IReadOnlyList<EmployeeOption> Employees { get; init; } = [];

    public int Page => Filter.Page;

    public int PageSize => Filter.PageSize;

    public string PageBaseUrl => "/admin/audit-logs";

    public string PageQuery(int page) => Filter.PageQuery(page);

    public string PageSizeQuery(int size) => Filter.PageSizeQuery(size);

    public ListState EmptyState => new(
        PastEnd: Filter.Page > 1,
        Filtered: Filter.HasFilters,
        Icon: "fa-clipboard-list",
        EmptyTitleKey: "Audit_NoneYet",
        EmptyHintKey: "Audit_NoneYetHint",
        FirstPageUrl: $"{PageBaseUrl}{Filter.PageQuery(1)}",
        ClearUrl: PageBaseUrl);
}
