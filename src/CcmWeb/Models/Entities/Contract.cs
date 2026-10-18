using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Contract
{
    public int ContractId { get; set; }

    public int CustomerId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly SignDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal TotalValue { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public virtual ICollection<ContractDetail> ContractDetails { get; set; } = new List<ContractDetail>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;
}
