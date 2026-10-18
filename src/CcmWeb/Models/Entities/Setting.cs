using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Setting
{
    public int SettingId { get; set; }

    public int? EmployeeId { get; set; }

    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public string DataType { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime UpdatedDate { get; set; }

    public virtual Employee? Employee { get; set; }
}
