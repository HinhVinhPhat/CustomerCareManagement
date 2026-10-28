using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// Small reads shared by the contact, care and feedback services: the active customers of a dropdown,
/// the staff list and the "this customer may be written to" check. Keeping them here stops the three
/// services from drifting apart; the rules themselves stay in the database.
/// </summary>
internal static class LookupQueries
{
    public static Task<List<CustomerOption>> ActiveCustomersAsync(
        ApplicationDbContext db, CancellationToken ct = default) =>
        db.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerOption(c.CustomerId, c.CustomerName))
            .ToListAsync(ct);

    public static Task<List<EmployeeOption>> StaffAsync(
        ApplicationDbContext db, CancellationToken ct = default) =>
        db.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeOption(e.EmployeeId, e.FullName))
            .ToListAsync(ct);

    /// <summary>
    /// A new interaction is written for an active customer only. The dropdown already carries active
    /// customers; this check covers a hand-crafted POST.
    /// </summary>
    public static async Task EnsureActiveCustomerAsync(
        ApplicationDbContext db, int customerId, CancellationToken ct = default)
    {
        var active = await db.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => (bool?)c.IsActive)
            .FirstOrDefaultAsync(ct);

        if (active is null)
        {
            throw new BusinessException("Error_CustomerNotFound");
        }

        if (active == false)
        {
            throw new BusinessException("Error_CustomerNotActive");
        }
    }
}
