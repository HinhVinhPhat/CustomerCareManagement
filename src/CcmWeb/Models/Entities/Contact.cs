using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Contact
{
    public int ContactId { get; set; }

    public int CustomerId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime ContactTime { get; set; }

    public string Channel { get; set; } = null!;

    public string? Content { get; set; }

    public string? Result { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;
}
