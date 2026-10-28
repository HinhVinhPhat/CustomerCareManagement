using System.ComponentModel.DataAnnotations;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One line the user is adding to a new contract.</summary>
public sealed class ContractLineInput
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
}

/// <summary>
/// The add-contract form (design.md 6.7). The detail lines live in a hidden <c>LinesJson</c> field as
/// a JSON array; the controller parses it into <see cref="Lines"/> before calling the service, so the
/// web side never builds SQL and the database receives exactly the shape <c>sp_AddContract</c> expects.
/// </summary>
public sealed class ContractFormViewModel
{
    [Required(ErrorMessage = "Validation_ContractCustomerRequired")]
    [Display(Name = "Contract_Customer")]
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Validation_ContractStaffRequired")]
    [Display(Name = "Contract_Staff")]
    public int? EmployeeId { get; set; }

    [Display(Name = "Contract_SignDate")]
    [DataType(DataType.Date)]
    public DateOnly? SignDate { get; set; }

    [Required(ErrorMessage = "Validation_ContractEndDateRequired")]
    [Display(Name = "Contract_EndDate")]
    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; }

    [Display(Name = "Contract_Status")]
    public string Status { get; set; } = WorkConstants.ContractStatus.Active;

    [StringLength(500, ErrorMessage = "Validation_ContractNoteTooLong")]
    [Display(Name = "Contract_Note")]
    public string? Note { get; set; }

    /// <summary>The lines serialised as JSON for the hidden field and for the procedure.</summary>
    public string LinesJson { get; set; } = "[]";

    /// <summary>The lines parsed back for re-rendering when validation fails.</summary>
    public IReadOnlyList<ContractLineInput> Lines { get; set; } = [];

    public IReadOnlyList<CustomerOption> Customers { get; set; } = [];
    public IReadOnlyList<EmployeeOption> Staff { get; set; } = [];
    public IReadOnlyList<ProductOption> Products { get; set; } = [];

    public bool IsPending => Status == WorkConstants.ContractStatus.Pending;
}

/// <summary>A customer a contract can belong to.</summary>
public sealed record CustomerOption(int CustomerId, string CustomerName);

/// <summary>A product the user can put on a contract line.</summary>
public sealed record ProductOption(int ProductId, string ProductName, string ProductType, decimal UnitPrice);

/// <summary>The contract detail page (design.md 6.8).</summary>
public sealed class ContractDetailViewModel
{
    public required ContractHeader Header { get; init; }
    public IReadOnlyList<ContractLineRow> Lines { get; init; } = [];
    public IReadOnlyList<ContractAuditRow> Audit { get; init; } = [];
    public DateOnly? SuggestedRenewDate { get; init; }
}

/// <summary>The header of a contract: the facts that do not change with its lines.</summary>
public sealed record ContractHeader(
    int ContractId,
    int CustomerId,
    string CustomerName,
    int EmployeeId,
    string EmployeeName,
    string? DepartmentName,
    DateOnly SignDate,
    DateOnly EndDate,
    int? DaysRemaining,
    decimal TotalValue,
    string Status,
    string? Note);
