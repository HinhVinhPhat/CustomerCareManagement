using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class ProductService
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string ProductType { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<ContractDetail> ContractDetails { get; set; } = new List<ContractDetail>();
}
