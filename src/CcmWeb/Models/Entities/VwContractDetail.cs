using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwContractDetail
{
    public int ContractId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string ProductType { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? LineTotal { get; set; }
}
