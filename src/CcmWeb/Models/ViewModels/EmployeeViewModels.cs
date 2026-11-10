using System.ComponentModel.DataAnnotations;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of <c>EMPLOYEE</c> together with the department name and whether it can sign in.</summary>
public sealed record EmployeeRow(
    int EmployeeId,
    string FullName,
    string Email,
    string Phone,
    string Gender,
    string Position,
    int DepartmentId,
    string DepartmentName,
    bool HasAccount);

/// <summary>A department of the filter and of the form's dropdown.</summary>
public sealed record DepartmentOption(int DepartmentId, string DepartmentName);

/// <summary>A row of the department card: its name, how many people it holds and who leads it.</summary>
public sealed record DepartmentRow(
    int DepartmentId,
    string DepartmentName,
    int EmployeeCount,
    string? HeadName);

/// <summary>The state of the employee list; every value is written back into the links.</summary>
public sealed class EmployeeFilter
{
    public const string SortByName = "Name";
    public const string SortByDepartment = "Department";

    public static readonly string[] SortFields = [SortByName, SortByDepartment];

    /// <summary>Keyword searched in the name, the e-mail and the phone; null = every employee.</summary>
    public string? Q { get; init; }

    public int? Department { get; init; }

    /// <summary>A value of <c>CHK_Employee_Position</c>; null = both levels.</summary>
    public string? Position { get; init; }

    public string Sort { get; init; } = SortByName;
    public bool Desc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Department is not null || Position is not null;

    public string SortQuery(string field) =>
        Build(field, Sort == field && !Desc, page: 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, page: 1, size);

    private string Build(string sort, bool desc, int page, int pageSize) =>
        new ListQuery()
            .Add("q", Q)
            .AddNumber("department", Department ?? 0, 0)
            .Add("position", Position)
            .Add("sort", sort, SortByName)
            .AddFlag("desc", desc)
            .AddNumber("page", page, 1)
            .AddNumber("pageSize", pageSize, WorkConstants.Settings.DefaultPageSize)
            .ToString();
}

/// <summary>The employee page: the filter, the rows and the department card.</summary>
public sealed class EmployeeListViewModel : IPagedList
{
    public required EmployeeFilter Filter { get; init; }

    public IReadOnlyList<EmployeeRow> Rows { get; init; } = [];

    public int TotalRows { get; init; }

    public string? ErrorKey { get; init; }

    public IReadOnlyList<DepartmentOption> Departments { get; init; } = [];

    public IReadOnlyList<DepartmentRow> DepartmentRows { get; init; } = [];

    /// <summary>Set when the department card could not be read, so the two blocks fail apart.</summary>
    public string? DepartmentErrorKey { get; init; }

    public int Page => Filter.Page;

    public int PageSize => Filter.PageSize;

    public string PageBaseUrl => "/admin/employees";

    public string PageQuery(int page) => Filter.PageQuery(page);

    public string PageSizeQuery(int size) => Filter.PageSizeQuery(size);

    public ListState EmptyState => new(
        PastEnd: Filter.Page > 1,
        Filtered: Filter.HasFilters,
        Icon: "fa-user-tie",
        EmptyTitleKey: "Employee_NoneYet",
        EmptyHintKey: "Employee_NoneYetHint",
        FirstPageUrl: $"{PageBaseUrl}{Filter.PageQuery(1)}",
        ClearUrl: PageBaseUrl,
        AddUrl: "/admin/employees/create",
        AddLabelKey: "Employee_Add");
}

/// <summary>The add / edit form of an employee; the department list fills the dropdown.</summary>
public sealed class EmployeeFormViewModel
{
    public int? EmployeeId { get; init; }

    public bool IsEdit => EmployeeId is not null;

    [Display(Name = "Employee_FullName")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(100, ErrorMessage = "Error_TooLong")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Employee_Email")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(100, ErrorMessage = "Error_TooLong")]
    [EmailAddress(ErrorMessage = "Error_EmployeeEmailInvalid")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Employee_Phone")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(15, ErrorMessage = "Error_TooLong")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Employee_Gender")]
    [Required(ErrorMessage = "Error_Required")]
    public string? Gender { get; set; }

    [Display(Name = "Employee_Position")]
    [Required(ErrorMessage = "Error_Required")]
    public string? Position { get; set; }

    [Display(Name = "Employee_Department")]
    [Range(1, int.MaxValue, ErrorMessage = "Error_Required")]
    public int? DepartmentId { get; set; }

    public IReadOnlyList<DepartmentOption> Departments { get; set; } = [];
}

/// <summary>The one-field form that adds a department or renames one.</summary>
public sealed class DepartmentFormModel
{
    public int? DepartmentId { get; init; }

    [Display(Name = "Department_Name")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(100, ErrorMessage = "Error_TooLong")]
    public string DepartmentName { get; set; } = string.Empty;
}
