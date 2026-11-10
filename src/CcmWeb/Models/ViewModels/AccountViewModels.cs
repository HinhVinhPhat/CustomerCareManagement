using System.ComponentModel.DataAnnotations;
using CcmWeb.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of <c>ACCOUNT</c>. The password hash never leaves the database.</summary>
public sealed record AccountRow(
    int AccountId,
    string Username,
    int EmployeeId,
    string EmployeeName,
    string Role,
    bool IsActive);

/// <summary>An employee who has no account yet, offered in the "create account" form.</summary>
public sealed record AccountlessEmployee(int EmployeeId, string FullName, string Position);

/// <summary>The state of the account list; every value is written back into the links.</summary>
public sealed class AccountFilter
{
    public const string SortByUsername = "Username";
    public const string SortByEmployee = "Employee";

    /// <summary>Values of the state dropdown; <c>null</c> means both.</summary>
    public const string StateActive = "active";
    public const string StateLocked = "locked";

    public static readonly string[] SortFields = [SortByUsername, SortByEmployee];

    /// <summary>Keyword searched in the username and the employee name.</summary>
    public string? Q { get; init; }

    /// <summary>A value of <c>ACCOUNT.Role</c>; null = every role.</summary>
    public string? Role { get; init; }

    public string? State { get; init; }

    public string Sort { get; init; } = SortByUsername;
    public bool Desc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Role is not null || State is not null;

    public string SortQuery(string field) =>
        Build(field, Sort == field && !Desc, page: 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, page: 1, size);

    private string Build(string sort, bool desc, int page, int pageSize) =>
        new ListQuery()
            .Add("q", Q)
            .Add("role", Role)
            .Add("state", State)
            .Add("sort", sort, SortByUsername)
            .AddFlag("desc", desc)
            .AddNumber("page", page, 1)
            .AddNumber("pageSize", pageSize, WorkConstants.Settings.DefaultPageSize)
            .ToString();
}

/// <summary>The account list page.</summary>
public sealed class AccountListViewModel : IPagedList
{
    public required AccountFilter Filter { get; init; }

    public IReadOnlyList<AccountRow> Rows { get; init; } = [];

    public int TotalRows { get; init; }

    public string? ErrorKey { get; init; }

    public int Page => Filter.Page;

    public int PageSize => Filter.PageSize;

    public string PageBaseUrl => "/admin/accounts";

    public string PageQuery(int page) => Filter.PageQuery(page);

    public string PageSizeQuery(int size) => Filter.PageSizeQuery(size);

    public ListState EmptyState => new(
        PastEnd: Filter.Page > 1,
        Filtered: Filter.HasFilters,
        Icon: "fa-key",
        EmptyTitleKey: "Account_NoneYet",
        EmptyHintKey: "Account_NoneYetHint",
        FirstPageUrl: $"{PageBaseUrl}{Filter.PageQuery(1)}",
        ClearUrl: PageBaseUrl,
        AddUrl: "/admin/accounts/create",
        AddLabelKey: "Account_Add");
}

/// <summary>Creates the first account of an employee; the password is hashed before it is written.</summary>
public sealed class AccountCreateViewModel
{
    [Display(Name = "Account_Employee")]
    [Range(1, int.MaxValue, ErrorMessage = "Error_Required")]
    public int? EmployeeId { get; set; }

    [Display(Name = "Account_Username")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(WorkConstants.Accounts.UsernameMaxLength, ErrorMessage = "Error_TooLong")]
    [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "Error_UsernameInvalid")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Account_Role")]
    [Required(ErrorMessage = "Error_Required")]
    public string? Role { get; set; }

    [Display(Name = "Password_New")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(WorkConstants.Accounts.PasswordMaxLength,
        MinimumLength = WorkConstants.Accounts.PasswordMinLength, ErrorMessage = "Error_PasswordTooShort")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Password_Confirm")]
    [Required(ErrorMessage = "Error_Required")]
    [Compare(nameof(Password), ErrorMessage = "Error_PasswordMismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Employees without an account, so a second one can never be created from the form.</summary>
    public IReadOnlyList<AccountlessEmployee> Employees { get; set; } = [];

    public string? ErrorKey { get; init; }
}

/// <summary>Changes the role of an account and locks or unlocks it.</summary>
public sealed class AccountEditViewModel
{
    public required int AccountId { get; init; }

    // Chỉ để hiển thị: biểu mẫu vẽ bằng <dd> nên không gửi lên. [ValidateNever] để ASP.NET Core
    // không tự thêm kiểm tra "bắt buộc" cho kiểu tham chiếu không nullable (xem B01, m06-admin).
    [ValidateNever]
    public required string Username { get; init; }

    [ValidateNever]
    public required string EmployeeName { get; init; }

    [Display(Name = "Account_Role")]
    [Required(ErrorMessage = "Error_Required")]
    public string? Role { get; set; }

    [Display(Name = "Account_IsActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>True for the signed-in account: the screen never offers to lock or demote itself.</summary>
    public bool IsSelf { get; init; }

    public string? ErrorKey { get; init; }
}

/// <summary>Sets a new password for another account. The old one is not asked for - a Manager resets it.</summary>
public sealed class ResetPasswordViewModel
{
    public required int AccountId { get; init; }

    // Chỉ để hiển thị - cùng lý do với AccountEditViewModel (xem B01, m06-admin).
    [ValidateNever]
    public required string Username { get; init; }

    [ValidateNever]
    public required string EmployeeName { get; init; }

    [Display(Name = "Password_New")]
    [Required(ErrorMessage = "Error_Required")]
    [StringLength(WorkConstants.Accounts.PasswordMaxLength,
        MinimumLength = WorkConstants.Accounts.PasswordMinLength, ErrorMessage = "Error_PasswordTooShort")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Password_Confirm")]
    [Required(ErrorMessage = "Error_Required")]
    [Compare(nameof(Password), ErrorMessage = "Error_PasswordMismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorKey { get; init; }
}
