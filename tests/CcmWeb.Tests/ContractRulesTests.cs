using System.Text.Json;
using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// The pure rules of the contract screens: the JSON shape <c>sp_AddContract</c> reads, the duplicate
/// product rule and which action a status allows. No database is needed for these.
/// </summary>
public class ContractRulesTests
{
    [Fact]
    public void The_detail_json_has_exactly_the_property_names_the_procedure_reads()
    {
        var json = ContractRules.BuildDetailJson(
        [
            new ContractLineInput { ProductId = 1, Quantity = 2, UnitPrice = 5000000m }
        ]);

        using var document = JsonDocument.Parse(json);
        var line = document.RootElement[0];

        Assert.Equal(1, line.GetProperty("ProductID").GetInt32());
        Assert.Equal(2, line.GetProperty("Quantity").GetInt32());
        Assert.Equal(5000000m, line.GetProperty("UnitPrice").GetDecimal());
        Assert.Equal(3, line.EnumerateObject().Count());
    }

    [Fact]
    public void A_missing_unit_price_stays_null_so_the_procedure_uses_the_catalogue_price()
    {
        var json = ContractRules.BuildDetailJson(
        [
            new ContractLineInput { ProductId = 3, Quantity = 1, UnitPrice = null }
        ]);

        using var document = JsonDocument.Parse(json);
        var price = document.RootElement[0].GetProperty("UnitPrice");

        Assert.Equal(JsonValueKind.Null, price.ValueKind);
    }

    [Fact]
    public void No_lines_serialise_to_an_empty_json_array() =>
        Assert.Equal("[]", ContractRules.BuildDetailJson([]));

    [Fact]
    public void The_json_round_trips_through_the_parser_the_controller_uses()
    {
        var lines = new[]
        {
            new ContractLineInput { ProductId = 2, Quantity = 4, UnitPrice = 15000000m },
            new ContractLineInput { ProductId = 7, Quantity = 1, UnitPrice = null }
        };

        var parsed = ContractRules.ParseLines(ContractRules.BuildDetailJson(lines));

        Assert.Equal(2, parsed.Count);
        Assert.Equal(2, parsed[0].ProductId);
        Assert.Equal(4, parsed[0].Quantity);
        Assert.Equal(15000000m, parsed[0].UnitPrice);
        Assert.Null(parsed[1].UnitPrice);
        Assert.Equal(7, parsed[1].ProductId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("[{\"ProductID\":")]
    [InlineData("not json")]
    public void An_empty_or_broken_hidden_field_counts_as_no_lines(string json) =>
        Assert.Empty(ContractRules.ParseLines(json));

    [Fact]
    public void The_same_product_twice_is_a_duplicate()
    {
        var lines = new[]
        {
            new ContractLineInput { ProductId = 1, Quantity = 1 },
            new ContractLineInput { ProductId = 2, Quantity = 1 },
            new ContractLineInput { ProductId = 1, Quantity = 3 }
        };

        Assert.True(ContractRules.HasDuplicateProduct(lines));
    }

    [Fact]
    public void Different_products_are_not_a_duplicate()
    {
        var lines = new[]
        {
            new ContractLineInput { ProductId = 1, Quantity = 1 },
            new ContractLineInput { ProductId = 2, Quantity = 1 }
        };

        Assert.False(ContractRules.HasDuplicateProduct(lines));
    }

    [Theory]
    [InlineData(WorkConstants.ContractStatus.Pending, true, false, true)]
    [InlineData(WorkConstants.ContractStatus.Active, false, true, true)]
    [InlineData(WorkConstants.ContractStatus.Expired, false, true, false)]
    [InlineData(WorkConstants.ContractStatus.Cancelled, false, false, false)]
    [InlineData("Unknown", false, false, false)]
    [InlineData(null, false, false, false)]
    public void Each_status_allows_only_the_actions_the_procedures_accept(
        string? status, bool canSign, bool canRenew, bool canCancel)
    {
        Assert.Equal(canSign, ContractRules.CanSign(status));
        Assert.Equal(canRenew, ContractRules.CanRenew(status));
        Assert.Equal(canCancel, ContractRules.CanCancel(status));
        Assert.Equal(canSign || canRenew || canCancel, ContractRules.HasAnyAction(status));
    }

    [Fact]
    public void Every_contract_status_has_a_badge_and_a_label_group()
    {
        foreach (var status in WorkConstants.ContractStatus.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(Badges.ForContractStatus(status)));
        }
    }
}
