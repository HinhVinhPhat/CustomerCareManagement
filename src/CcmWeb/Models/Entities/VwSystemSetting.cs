using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwSystemSetting
{
    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public string DataType { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime UpdatedDate { get; set; }
}
