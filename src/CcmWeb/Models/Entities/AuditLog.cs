using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class AuditLog
{
    public long LogId { get; set; }

    public DateTime LogTime { get; set; }

    public int? EmployeeId { get; set; }

    public string TableName { get; set; } = null!;

    public int RecordId { get; set; }

    public string Action { get; set; } = null!;

    public string? Detail { get; set; }

    public virtual Employee? Employee { get; set; }
}
