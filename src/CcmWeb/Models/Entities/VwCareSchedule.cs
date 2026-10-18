using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwCareSchedule
{
    public int CareId { get; set; }

    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string? Phone { get; set; }

    public int EmployeeId { get; set; }

    public string FullName { get; set; } = null!;

    public string CareType { get; set; } = null!;

    public string? Content { get; set; }

    public DateOnly? NextAppointmentDate { get; set; }

    public string ScheduleStatus { get; set; } = null!;
}
