using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

public sealed class AccountService(
    ApplicationDbContext db,
    IPasswordHasher<Account> passwordHasher,
    ILogger<AccountService> logger) : IAccountService
{
    // Only used as the user argument of PasswordHasher, which ignores it.
    private static readonly Account HashingProbe = new();

    public async Task<LoginOutcome> AuthenticateAsync(string username, string password)
    {
        var account = await db.Accounts
            .Include(a => a.Employee)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Username == username);

        if (account is null)
        {
            // Hash something anyway so that the response time does not tell an attacker whether
            // the username exists.
            passwordHasher.HashPassword(HashingProbe, password);
            return new LoginOutcome(LoginResult.InvalidCredentials, null);
        }

        var (verified, needsRehash) = VerifyPassword(account, password);
        if (!verified)
        {
            logger.LogInformation("Failed sign-in attempt for username {Username}", username);
            return new LoginOutcome(LoginResult.InvalidCredentials, null);
        }

        if (!account.IsActive)
        {
            // Reported only after a correct password, otherwise anyone could probe which accounts are locked.
            return new LoginOutcome(LoginResult.Locked, null);
        }

        if (needsRehash)
        {
            await RehashAsync(account.AccountId, password);
        }

        return new LoginOutcome(LoginResult.Success, new AuthenticatedUser(
            account.AccountId,
            account.EmployeeId,
            account.Username,
            account.Employee.FullName,
            account.Role));
    }

    private (bool Verified, bool NeedsRehash) VerifyPassword(Account account, string password) =>
        PasswordRules.Verify(passwordHasher, account, password);

    /// <summary>ACCOUNT has no trigger, so a plain UPDATE through EF is safe here.</summary>
    private async Task RehashAsync(int accountId, string password)
    {
        var hash = passwordHasher.HashPassword(HashingProbe, password);

        await db.Accounts
            .Where(a => a.AccountId == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.PasswordHash, hash));

        logger.LogInformation("Upgraded AccountID {AccountID} from the demo SHA2 hash to PasswordHasher", accountId);
    }
}
