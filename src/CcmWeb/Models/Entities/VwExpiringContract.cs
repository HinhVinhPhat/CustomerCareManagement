using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwExpiringContract
{
    public int ContractId { get; set; }

    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string? Phone { get; set; }

    public int EmployeeId { get; set; }

    public string FullName { get; set; } = null!;

    public DateOnly EndDate { get; set; }

    public int? DaysRemaining { get; set; }

    public decimal TotalValue { get; set; }
}
