using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwCustomerRevenue
{
    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string CustomerType { get; set; } = null!;

    public string Tier { get; set; } = null!;

    public int? OwnerEmployeeId { get; set; }

    public bool IsActive { get; set; }

    public int? ContractCount { get; set; }

    public decimal TotalValue { get; set; }
}
