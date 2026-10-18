using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Care
{
    public int CareId { get; set; }

    public int CustomerId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly CareDate { get; set; }

    public string CareType { get; set; } = null!;

    public string? Content { get; set; }

    public string? Result { get; set; }

    public byte? SatisfactionScore { get; set; }

    public DateOnly? NextAppointmentDate { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;
}
