namespace CcmWeb.Models.Entities;

/// <summary>
/// One row of the commune/ward catalogue (WARD), the second level of the current two-level
/// administrative structure. Old three-level addresses are kept as free text in
/// <see cref="Customer.Address"/> instead of being rewritten.
/// </summary>
public partial class Ward
{
    public int WardCode { get; set; }

    public int ProvinceCode { get; set; }

    public string WardName { get; set; } = null!;

    public string DivisionType { get; set; } = null!;

    public bool IsActive { get; set; }
}
