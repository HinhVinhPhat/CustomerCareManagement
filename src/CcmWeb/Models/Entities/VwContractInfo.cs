using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwContractInfo
{
    public int ContractId { get; set; }

    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public int EmployeeId { get; set; }

    public string FullName { get; set; } = null!;

    public string? DepartmentName { get; set; }

    public DateOnly SignDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int? DaysRemaining { get; set; }

    public decimal TotalValue { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }
}
