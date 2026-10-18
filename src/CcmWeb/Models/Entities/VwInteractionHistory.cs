using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwInteractionHistory
{
    public int CustomerId { get; set; }

    public DateTime? LogTime { get; set; }

    public string Category { get; set; } = null!;

    public string SubType { get; set; } = null!;

    public string? Content { get; set; }

    public string? Result { get; set; }

    public int? EmployeeId { get; set; }

    public string? FullName { get; set; }
}
