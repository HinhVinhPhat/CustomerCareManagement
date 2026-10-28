using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the contact list plus the number of matches before paging.</summary>
public sealed record ContactSearchResult(IReadOnlyList<ContactRow> Rows, int TotalRows);

/// <summary>
/// The contact screens (design.md 6.9). A contact is history: it is added, never edited or deleted.
/// The employee comes from the signed-in claim and the time from the server clock, never from the
/// form.
/// </summary>
public interface IContactService
{
    Task<ContactSearchResult> SearchAsync(ContactFilter filter, CancellationToken ct = default);

    Task<int> CreateAsync(ContactFormViewModel form, int employeeId, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default);
}
