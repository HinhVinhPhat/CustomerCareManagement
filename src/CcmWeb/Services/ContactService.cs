using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The contact log. Filtering, sorting and paging run in SQL over <c>CONTACT</c>; nothing is rebuilt
/// from a view that already exists, because this table has no view of its own.
/// </summary>
public sealed class ContactService(ApplicationDbContext db, ILogger<ContactService> logger) : IContactService
{
    public async Task<ContactSearchResult> SearchAsync(
        ContactFilter filter, CancellationToken ct = default)
    {
        var query = db.Contacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(c => c.Customer.CustomerName.Contains(q));
        }

        if (filter.Staff is { } staff)
        {
            query = query.Where(c => c.EmployeeId == staff);
        }

        if (filter.Channel is not null)
        {
            query = query.Where(c => c.Channel == filter.Channel);
        }

        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(c => c.ContactTime >= start);
        }

        if (filter.To is { } to)
        {
            // Half-open range: everything before the next midnight belongs to the "to" day.
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(c => c.ContactTime < end);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (ContactFilter.SortByCustomer, false) => query
                .OrderBy(c => c.Customer.CustomerName).ThenByDescending(c => c.ContactTime),
            (ContactFilter.SortByCustomer, true) => query
                .OrderByDescending(c => c.Customer.CustomerName).ThenByDescending(c => c.ContactTime),
            (ContactFilter.SortByStaff, false) => query
                .OrderBy(c => c.Employee.FullName).ThenByDescending(c => c.ContactTime),
            (ContactFilter.SortByStaff, true) => query
                .OrderByDescending(c => c.Employee.FullName).ThenByDescending(c => c.ContactTime),
            (ContactFilter.SortByChannel, false) => query
                .OrderBy(c => c.Channel).ThenByDescending(c => c.ContactTime),
            (ContactFilter.SortByChannel, true) => query
                .OrderByDescending(c => c.Channel).ThenByDescending(c => c.ContactTime),
            (_, false) => query.OrderBy(c => c.ContactTime).ThenBy(c => c.ContactId),
            _ => query.OrderByDescending(c => c.ContactTime).ThenByDescending(c => c.ContactId),
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new ContactRow(
                c.ContactId,
                c.ContactTime,
                c.CustomerId,
                c.Customer.CustomerName,
                c.EmployeeId,
                c.Employee.FullName,
                c.Channel,
                c.Content,
                c.Result))
            .ToListAsync(ct);

        return new ContactSearchResult(rows, totalRows);
    }

    public async Task<int> CreateAsync(
        ContactFormViewModel form, int employeeId, CancellationToken ct = default)
    {
        var customerId = form.CustomerId!.Value;
        await LookupQueries.EnsureActiveCustomerAsync(db, customerId, ct);

        var contact = new Contact
        {
            CustomerId = customerId,
            EmployeeId = employeeId,
            ContactTime = DateTime.Now,
            Channel = form.Channel!,
            Content = Trimmed(form.Content),
            Result = Trimmed(form.Result)
        };

        db.Contacts.Add(contact);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Recording a contact failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }

        return contact.ContactId;
    }

    public async Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default) =>
        await LookupQueries.ActiveCustomersAsync(db, ct);

    public async Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default) =>
        await LookupQueries.StaffAsync(db, ct);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
