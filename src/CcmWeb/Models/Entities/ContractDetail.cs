using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class ContractDetail
{
    public int ContractId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Contract Contract { get; set; } = null!;

    public virtual ProductService Product { get; set; } = null!;
}
