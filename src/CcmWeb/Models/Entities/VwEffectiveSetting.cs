using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwEffectiveSetting
{
    public int EmployeeId { get; set; }

    public string FullName { get; set; } = null!;

    public int DepartmentId { get; set; }

    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public int IsOverridden { get; set; }

    public string DataType { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
