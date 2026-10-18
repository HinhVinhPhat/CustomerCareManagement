using CcmWeb.Helpers;
using CcmWeb.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace CcmWeb.Tests;

/// <summary>The sample data stores SHA2_256 of the demo password; these tests pin that format.</summary>
public class LegacyHashTests
{
    private const string StoredHash = "EC278A38901287B2771A13739520384D43E4B078F78AFFE702DEF108774CCE24";

    [Fact]
    public void The_demo_hash_matches_the_value_loaded_by_the_sample_data() =>
        Assert.Equal(StoredHash, Utils.ComputeLegacySha256("123456"));

    [Fact]
    public void The_correct_demo_password_is_accepted() =>
        Assert.True(Utils.VerifyLegacySha256("123456", StoredHash));

    [Fact]
    public void A_wrong_password_is_rejected() =>
        Assert.False(Utils.VerifyLegacySha256("1234567", StoredHash));

    [Fact]
    public void The_stored_hash_is_recognised_as_the_legacy_form() =>
        Assert.True(Utils.IsLegacySha256Hash(StoredHash));

    [Fact]
    public void A_PasswordHasher_hash_is_not_mistaken_for_the_legacy_form()
    {
        var hash = new PasswordHasher<Account>().HashPassword(new Account(), "123456");

        Assert.False(Utils.IsLegacySha256Hash(hash));
    }
}
