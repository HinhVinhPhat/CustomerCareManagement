using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string DepartmentName { get; set; } = null!;

    public virtual Employee? Employee { get; set; }
}
