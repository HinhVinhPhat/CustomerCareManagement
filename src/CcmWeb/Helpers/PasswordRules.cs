using CcmWeb.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace CcmWeb.Helpers;

/// <summary>
/// One place for the password rules of M06. The length is a decision of the application, because the
/// database only stores the hash; the check that a typed password matches a stored one accepts both
/// forms - the demo SHA2 hash and the PasswordHasher hash, which the sign-in upgrade replaces.
/// </summary>
public static class PasswordRules
{
    /// <summary>8 to 100 characters, no other requirement: length is what actually helps.</summary>
    public static bool IsAcceptable(string? password) =>
        password is { Length: >= WorkConstants.Accounts.PasswordMinLength }
        && password.Length <= WorkConstants.Accounts.PasswordMaxLength;

    /// <summary>
    /// Verifies a password against the stored hash. <c>NeedsRehash</c> is true when the row still
    /// carries the demo SHA2 hash: the caller writes the PasswordHasher hash at that moment.
    /// </summary>
    public static (bool Verified, bool NeedsRehash) Verify(
        IPasswordHasher<Account> hasher, Account account, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return (false, false);
        }

        if (Utils.IsLegacySha256Hash(account.PasswordHash))
        {
            return (Utils.VerifyLegacySha256(password, account.PasswordHash), true);
        }

        return hasher.VerifyHashedPassword(account, account.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => (true, false),
            PasswordVerificationResult.SuccessRehashNeeded => (true, true),
            _ => (false, false)
        };
    }
}
