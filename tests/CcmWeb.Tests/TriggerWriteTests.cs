using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Tests;

/// <summary>
/// Writes through EF against tables that carry triggers. Without <c>UseSqlOutputClause(false)</c>
/// these calls fail with SQL error 334, so they also prove that configuration.
/// </summary>
[Collection("database")]
public class TriggerWriteTests
{
    [Fact]
    public async Task A_new_customer_can_be_saved_even_though_the_table_has_a_trigger()
    {
        await using var db = CreateContext();

        var customer = NewCustomer("Trigger Test Ltd", "trigger.test@customercare.com");
        db.Customers.Add(customer);

        await db.SaveChangesAsync();

        Assert.True(customer.CustomerId > 0);
    }

    [Fact]
    public async Task A_duplicate_email_is_reported_as_a_business_message_key()
    {
        await using var db = CreateContext();

        db.Customers.Add(NewCustomer("First Ltd", "duplicate@customercare.com"));
        await db.SaveChangesAsync();

        db.Customers.Add(NewCustomer("Second Ltd", "duplicate@customercare.com"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal("Error_CustomerEmailTaken", Utils.TranslateSqlError(exception));
    }

    [Fact]
    public async Task A_gender_on_a_business_customer_is_reported_as_a_business_message_key()
    {
        await using var db = CreateContext();

        var customer = NewCustomer("Business With Gender Ltd", "business.gender@customercare.com");
        customer.CustomerType = "Business";
        customer.Gender = "Male";
        db.Customers.Add(customer);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal("Error_BusinessCustomerNoGender", Utils.TranslateSqlError(exception));
    }

    private static Customer NewCustomer(string name, string email) => new()
    {
        CustomerName = name,
        CustomerType = "Individual",
        Email = email,
        Phone = "0900000000",
        Gender = "Male",
        CreatedDate = new DateOnly(2026, 1, 1),
        Tier = "Standard",
        IsActive = true
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
