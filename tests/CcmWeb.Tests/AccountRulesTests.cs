using CcmWeb.Helpers;
using CcmWeb.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace CcmWeb.Tests;

/// <summary>
/// How a typed password is checked against what the database stores. Two forms exist: the demo SHA2
/// hash of the sample data and the PasswordHasher hash the application writes. Both must be accepted,
/// and the demo one has to be replaced at that moment so an old account is upgraded instead of
/// rejected.
/// </summary>
public class AccountRulesTests
{
    private static readonly PasswordHasher<Account> Hasher = new();

    [Fact]
    public void A_password_hashed_by_the_application_verifies_and_needs_no_rehash()
    {
        var account = new Account { PasswordHash = Hasher.HashPassword(new Account(), "MatKhau123") };

        var (verified, needsRehash) = PasswordRules.Verify(Hasher, account, "MatKhau123");

        Assert.True(verified);
        Assert.False(needsRehash);
    }

    [Fact]
    public void A_wrong_password_does_not_verify()
    {
        var account = new Account { PasswordHash = Hasher.HashPassword(new Account(), "MatKhau123") };

        var (verified, needsRehash) = PasswordRules.Verify(Hasher, account, "MatKhau124");

        Assert.False(verified);
        Assert.False(needsRehash);
    }

    [Fact]
    public void A_demo_sha2_password_verifies_and_asks_for_a_rehash()
    {
        var account = new Account { PasswordHash = Utils.ComputeLegacySha256("123456") };

        var (verified, needsRehash) = PasswordRules.Verify(Hasher, account, "123456");

        Assert.True(verified);
        Assert.True(needsRehash);
    }

    [Fact]
    public void A_wrong_demo_password_does_not_verify()
    {
        var account = new Account { PasswordHash = Utils.ComputeLegacySha256("123456") };

        var (verified, _) = PasswordRules.Verify(Hasher, account, "123457");

        Assert.False(verified);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void An_empty_password_never_verifies(string? password)
    {
        var account = new Account { PasswordHash = Hasher.HashPassword(new Account(), "MatKhau123") };

        var (verified, needsRehash) = PasswordRules.Verify(Hasher, account, password);

        Assert.False(verified);
        Assert.False(needsRehash);
    }

    [Fact]
    public void The_demo_hash_is_recognised_and_the_application_hash_is_not()
    {
        Assert.True(Utils.IsLegacySha256Hash(Utils.ComputeLegacySha256("123456")));
        Assert.False(Utils.IsLegacySha256Hash(Hasher.HashPassword(new Account(), "MatKhau123")));
    }
}
