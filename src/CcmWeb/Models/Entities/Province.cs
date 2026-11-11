namespace CcmWeb.Models.Entities;

/// <summary>
/// One row of the Vietnamese province/city catalogue (PROVINCE). The data comes from
/// <c>database/address/vn-provinces.json</c> and is loaded by <c>05_AddressData.sql</c>; the
/// application only reads it. <c>IsActive = 0</c> means the source no longer lists the row, but old
/// customers may still point at it.
/// </summary>
public partial class Province
{
    public int ProvinceCode { get; set; }

    public string ProvinceName { get; set; } = null!;

    public string DivisionType { get; set; } = null!;

    public bool IsActive { get; set; }
}
