namespace CcmWeb.Models.Entities;

/// <summary>
/// The structured part of a customer address, added without touching the scaffolded
/// <c>Customer.cs</c>. Both columns are optional: rows created before the address catalogue existed
/// keep only the free-text <see cref="Customer.Address"/> and are never rewritten.
/// </summary>
public partial class Customer
{
    public int? ProvinceCode { get; set; }

    public int? WardCode { get; set; }
}
