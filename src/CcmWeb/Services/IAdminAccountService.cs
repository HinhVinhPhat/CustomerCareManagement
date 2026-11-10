using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the account list plus the number of matches before paging.</summary>
public sealed record AccountSearchResult(IReadOnlyList<AccountRow> Rows, int TotalRows);

/// <summary>
/// Login accounts (design.md 6.13). A Manager creates one account per employee, sets the role, locks
/// and unlocks it and resets the password; the hash itself never leaves the database and is never
/// shown. The signed-in Manager can neither lock nor demote their own account, and the last active
/// Manager can never be removed - otherwise nobody could let themselves back in.
/// </summary>
public interface IAdminAccountService
{
    Task<AccountSearchResult> SearchAsync(AccountFilter filter, CancellationToken ct = default);

    /// <summary>Employees without an account, i.e. the only people the create form may offer.</summary>
    Task<IReadOnlyList<AccountlessEmployee>> GetAccountlessEmployeesAsync(CancellationToken ct = default);

    Task<int> CreateAsync(AccountCreateViewModel form, CancellationToken ct = default);

    Task<AccountEditViewModel?> GetAsync(int accountId, int actorAccountId, CancellationToken ct = default);

    Task UpdateAsync(int accountId, AccountEditViewModel form, int actorAccountId, CancellationToken ct = default);

    Task<ResetPasswordViewModel?> GetForResetAsync(int accountId, CancellationToken ct = default);

    Task ResetPasswordAsync(int accountId, ResetPasswordViewModel form, CancellationToken ct = default);
}
